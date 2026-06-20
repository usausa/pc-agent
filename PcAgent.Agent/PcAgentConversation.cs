namespace PcAgent.Agent;

using System.Runtime.CompilerServices;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

using OpenAI.Chat;

using PcAgent.Agent.Options;
using PcAgent.Agent.Tools;

// 実 LLM(Microsoft Agent Framework)を用いた対話。RunStreamingAsync を AgentEvent へ変換する。
public sealed class PcAgentConversation : IAgentConversation
{
    private readonly AIAgent? agent;

    public PcAgentConversation(IOptions<LlmOptions> options, PcInfoTools tools)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tools);

        var llm = options.Value;
        AgentName = "PcAgent";
        ModelName = String.IsNullOrWhiteSpace(llm.Model) ? "(unset)" : llm.Model;

        var chatClient = ChatClientFactory.TryCreate(llm);
        if (chatClient is not null)
        {
            agent = chatClient.AsAIAgent(
                instructions:
                    "あなたはこの Windows PC の状態を調べるアシスタントです。" +
                    "PC の情報(CPU/GPU/メモリ/ディスク/SMART/システム等)に関する質問には、必ずツールで実際の値を取得し、" +
                    "結果を簡潔に日本語でまとめて答えてください。値が取得できない場合は推測せず不明と答えてください。",
                name: AgentName,
                tools:
                [
                    AIFunctionFactory.Create(tools.GetPcInfo),
                    AIFunctionFactory.Create(tools.ListCategories),
                ]);
        }
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
}
