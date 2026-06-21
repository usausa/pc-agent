namespace PcAgent.Agent;

using System.IO;
using System.Runtime.CompilerServices;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

using PcAgent.Agent.Options;
using PcAgent.Agent.Rag;
using PcAgent.Agent.Tools;

// 実 LLM(Microsoft Agent Framework)を用いた対話。ツール + RAG + コンテキスト注入を構成する。
public sealed class PcAgentConversation : IAgentConversation
{
    private readonly AIAgent? agent;

    public PcAgentConversation(IOptions<LlmOptions> options, IOptions<RagOptions> ragOptions, PcInfoTools tools)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ragOptions);
        ArgumentNullException.ThrowIfNull(tools);

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
            var store = new KnowledgeStore(Resolve(rag.KnowledgePath));
            providers.Add(BuildRagProvider(store));
        }

        var agentOptions = new ChatClientAgentOptions
        {
            Name = AgentName,
            ChatOptions = new ChatOptions
            {
                Instructions =
                    "あなたはこの Windows PC の状態を調べるアシスタントです。" +
                    "PC の情報に関する質問には必ずツールで実際の値を取得し、結果を簡潔に日本語でまとめて答えてください。" +
                    "ナレッジ(参考情報)が提供された場合はそれを根拠として活用し、可能なら出典(SourceName)を示してください。" +
                    "値や根拠が得られない場合は推測せず『不明』と答えてください。",
                Tools =
                [
                    AIFunctionFactory.Create(tools.GetPcInfo),
                    AIFunctionFactory.Create(tools.ListCategories),
                ],
            },
            AIContextProviders = providers,
        };

        agent = chatClient.AsAIAgent(agentOptions);
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

        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);

        await foreach (var update in agent.RunStreamingAsync(userMessage, cancellationToken: cancellationToken).ConfigureAwait(false))
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
}
