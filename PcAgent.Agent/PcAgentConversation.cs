namespace PcAgent.Agent;

using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

using PcAgent.Agent.Options;
using PcAgent.Agent.Rag;
using PcAgent.Agent.Tools;

using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

// 実 LLM(Microsoft Agent Framework)を用いた対話。ツール + RAG + コンテキスト注入 + HITL 承認 + 計測を構成する。
public sealed partial class PcAgentConversation : IAgentConversation
{
    private readonly AIAgent? agent;

    private readonly IToolApprovalHandler approvalHandler;

    public PcAgentConversation(
        IOptions<LlmOptions> options,
        IOptions<RagOptions> ragOptions,
        PcInfoTools tools,
        IToolApprovalHandler handler,
        AgentTelemetry telemetry,
        ILogger<PcAgentConversation> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ragOptions);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(telemetry);

        approvalHandler = handler;

        var llm = options.Value;
        AgentName = "PcAgent";
        ModelName = String.IsNullOrWhiteSpace(llm.Model) ? "(unset)" : llm.Model;

        var chatClient = ChatClientFactory.TryCreate(llm);
        if (chatClient is null)
        {
            return;
        }

        var providers = new List<AIContextProvider> { new PcContextProvider() };

        var rag = ragOptions.Value;
        if (rag.Enabled)
        {
            providers.Add(BuildRagProvider(new KnowledgeStore(Resolve(rag.KnowledgePath))));
        }

        var agentOptions = new ChatClientAgentOptions
        {
            Name = AgentName,
            ChatOptions = new ChatOptions
            {
                Instructions =
                    "あなたはこの Windows PC の状態を調べ、必要に応じて修復するアシスタントです。" +
                    "情報の質問には必ずツールで実際の値を取得し、簡潔に日本語でまとめて答えてください。" +
                    "ナレッジ(参考情報)が提供された場合はそれを根拠として活用し、可能なら出典(SourceName)を示してください。" +
                    "一時ファイル削除や bin/obj 削除などの修復は、承認が必要なツールとして用意されています。" +
                    "値や根拠が得られない場合は推測せず『不明』と答えてください。",
                Tools =
                [
                    AIFunctionFactory.Create(tools.GetPcInfo),
                    AIFunctionFactory.Create(tools.ListCategories),
                    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(MaintenanceTools.CleanTemporaryFiles)),
                    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(MaintenanceTools.CleanBinObj)),
                ],
            },
            AIContextProviders = providers,
        };

        var enableSensitiveData = telemetry.EnableSensitiveData;
        agent = chatClient.AsAIAgent(agentOptions)
            .AsBuilder()
            .Use(async (messages, session, runOptions, next, cancellationToken) =>
            {
                var stopwatch = Stopwatch.StartNew();
                await next(messages, session, runOptions, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                LogAgentRun(logger, stopwatch.ElapsedMilliseconds);
            })
            .Use(async (agentInstance, context, next, cancellationToken) =>
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await next(context, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                LogToolInvocation(logger, context.Function.Name, stopwatch.ElapsedMilliseconds);
                return result;
            })
            .UseOpenTelemetry(AgentTelemetry.SourceName, config => config.EnableSensitiveData = enableSensitiveData)
            .Build();
    }

    public string AgentName { get; }

    public string ModelName { get; }

    public bool IsConfigured => agent is not null;

    public async IAsyncEnumerable<AgentEvent> SendAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (agent is null)
        {
            yield return new ResponseCompleted();
            yield break;
        }

        var session = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);
        IEnumerable<ChatMessage> input = [new ChatMessage(ChatRole.User, userMessage)];

        while (true)
        {
            var approvals = new List<ToolApprovalRequestContent>();

            await foreach (var update in agent.RunStreamingAsync(input, session, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case FunctionCallContent call:
                            toolNames[call.CallId] = call.Name;
                            yield return new ToolCallStarted(call.Name, FormatArguments(call));
                            break;
                        case FunctionResultContent result:
                            var name = toolNames.TryGetValue(result.CallId, out var resolved) ? resolved : result.CallId;
                            yield return new ToolCallCompleted(name, result.Result?.ToString() ?? String.Empty);
                            break;
                        case ToolApprovalRequestContent approval:
                            approvals.Add(approval);
                            break;
                        default:
                            break;
                    }
                }

                var text = update.ToString();
                if (!String.IsNullOrEmpty(text))
                {
                    yield return new TextDelta(text);
                }
            }

            if (approvals.Count == 0)
            {
                break;
            }

            var responses = new List<AIContent>();
            foreach (var approval in approvals)
            {
                var call = approval.ToolCall as FunctionCallContent;
                var toolName = call?.Name ?? "tool";
                var arguments = call is not null ? FormatArguments(call) : String.Empty;
                var approved = await approvalHandler.ApproveAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);
                responses.Add(approval.CreateResponse(approved, approved ? "approved by user" : "rejected by user"));
            }

            input = [new ChatMessage(ChatRole.User, responses)];
        }

        yield return new ResponseCompleted();
    }

    private static string FormatArguments(FunctionCallContent call) =>
        call.Arguments is { Count: > 0 } arguments
            ? String.Join(", ", arguments.Select(static x => x.Key + "=" + x.Value))
            : "(no args)";

    private static TextSearchProvider BuildRagProvider(KnowledgeStore store) =>
        new(
            (query, _) =>
            {
                var results = store.Search(query)
                    .Select(static doc => new TextSearchProvider.TextSearchResult { SourceName = doc.Source, Text = doc.Text })
                    .ToList();
                return Task.FromResult<IEnumerable<TextSearchProvider.TextSearchResult>>(results);
            },
            new TextSearchProviderOptions());

    private static string Resolve(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Agent run finished in {ElapsedMs} ms")]
    private static partial void LogAgentRun(ILogger logger, long elapsedMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Tool {Tool} invoked in {ElapsedMs} ms")]
    private static partial void LogToolInvocation(ILogger logger, string tool, long elapsedMs);
}
