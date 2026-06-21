namespace PcAgent.Tui.Commands;

using PcAgent.Agent;
using PcAgent.Tui.Rendering;
using PcAgent.Tui.Repl;

using Smart.CommandLine.Hosting;

using Spectre.Console;

// ルートコマンド。引数なしで対話 REPL、--ask で単発質問をストリーミング表示する。
public sealed class RootCommandHandler(IAgentConversation conversation, ReplSession repl) : ICommandHandler
{
    // 単発で投げる質問。
    [Option<string>("--ask", "-a", Description = "Ask a single question and exit")]
    public string? Ask { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var ask = Ask;
        if (String.IsNullOrWhiteSpace(ask))
        {
            await repl.RunAsync(context.CancellationToken);
            return;
        }

        if (!conversation.IsConfigured)
        {
            var message = "LLM 接続が未設定です。Llm:Endpoint / Llm:ApiKey / Llm:Model を appsettings.json・user-secrets・環境変数(Llm__*) のいずれかで設定してください。";
            await Console.Error.WriteLineAsync(message);
            context.ExitCode = 1;
            return;
        }

        var youLine = $"[bold green]> you[/]  [silver]{Markup.Escape(ask)}[/]";
        AnsiConsole.MarkupLine(youLine);
        await ConversationRenderer.StreamAsync(conversation, ask, context.CancellationToken);
    }
}
