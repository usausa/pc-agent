namespace PcAgent.Tui.Commands;

using Microsoft.Extensions.Logging;

using PcAgent.Agent;

using Smart.CommandLine.Hosting;

// ルートコマンド。引数なしで対話 REPL(フェーズ5)、--ask で単発質問をストリーミング表示する。
public sealed class RootCommandHandler(IAgentConversation conversation, ILogger<RootCommandHandler> log) : ICommandHandler
{
    // 単発で投げる質問。
    [Option<string>("--ask", "-a", Description = "Ask a single question and exit")]
    public string? Ask { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var ask = Ask;
        if (String.IsNullOrWhiteSpace(ask))
        {
            log.LogInformation("Interactive REPL is not available yet (arrives in phase 5). Run with --help, or --ask \"<question>\".");
            return;
        }

        if (!conversation.IsConfigured)
        {
            var message = "LLM 接続が未設定です。Llm:Endpoint / Llm:ApiKey / Llm:Model を appsettings.json・user-secrets・環境変数(Llm__*) のいずれかで設定してください。";
            await Console.Error.WriteLineAsync(message);
            context.ExitCode = 1;
            return;
        }

        await Console.Out.WriteLineAsync($"> you: {ask}");
        await foreach (var agentEvent in conversation.SendAsync(ask, context.CancellationToken))
        {
            switch (agentEvent)
            {
                case ToolCallStarted started:
                    await Console.Out.WriteLineAsync();
                    await Console.Out.WriteLineAsync($"  [tool] {started.Name}({started.Arguments})");
                    break;
                case TextDelta delta:
                    await Console.Out.WriteAsync(delta.Text);
                    break;
                case ResponseCompleted:
                    await Console.Out.WriteLineAsync();
                    break;
                default:
                    break;
            }
        }
    }
}
