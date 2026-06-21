namespace PcAgent.Tui.Repl;

using PcAgent.Agent;
using PcAgent.Diagnostics.Collectors;
using PcAgent.Tui.Rendering;

using Spectre.Console;

// 入力を先頭文字で振り分ける: / コマンド / @ 情報注入 / ! シェル / その他=エージェント。
public sealed class InputDispatcher(
    IAgentConversation conversation,
    IEnumerable<ICollector> collectors,
    ShellRunner shell)
{
    public async Task DispatchAsync(string line, SlashCommandContext context, CancellationToken cancellationToken)
    {
        switch (line[0])
        {
            case '/':
                await DispatchSlashAsync(line, context, cancellationToken);
                break;
            case '@':
                await DispatchAtAsync(line, cancellationToken);
                break;
            case '!':
                await shell.RunAsync(line[1..].Trim(), cancellationToken);
                break;
            default:
                await DispatchAgentAsync(line, cancellationToken);
                break;
        }
    }

    private static async ValueTask DispatchSlashAsync(string line, SlashCommandContext context, CancellationToken cancellationToken)
    {
        var (name, args) = SplitHead(line[1..]);
        var command = context.Commands.FirstOrDefault(c => String.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            var unknown = $"[yellow]Unknown command: /{Markup.Escape(name)}  ( /help )[/]";
            AnsiConsole.MarkupLine(unknown);
            return;
        }

        await command.ExecuteAsync(context, args, cancellationToken);
    }

    private async ValueTask DispatchAtAsync(string line, CancellationToken cancellationToken)
    {
        var (name, question) = SplitHead(line[1..]);
        var collector = collectors.FirstOrDefault(c => String.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (collector is null)
        {
            var unknown = $"[yellow]Unknown source: @{Markup.Escape(name)}[/]";
            AnsiConsole.MarkupLine(unknown);
            return;
        }

        var result = await collector.CollectAsync(cancellationToken);
        if (String.IsNullOrEmpty(question))
        {
            InfoRenderer.Render([result]);
            return;
        }

        if (!conversation.IsConfigured)
        {
            NotConfigured();
            InfoRenderer.Render([result]);
            return;
        }

        var message = question + "\n\n[参考情報: " + collector.DisplayName + "]\n" + CollectorText.ToText(result);
        await ConversationRenderer.StreamAsync(conversation, message, cancellationToken);
    }

    private async ValueTask DispatchAgentAsync(string line, CancellationToken cancellationToken)
    {
        if (!conversation.IsConfigured)
        {
            NotConfigured();
            return;
        }

        await ConversationRenderer.StreamAsync(conversation, line, cancellationToken);
    }

    private static (string Name, string Remainder) SplitHead(string text)
    {
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (text, String.Empty) : (text[..space], text[(space + 1)..].Trim());
    }

    private static void NotConfigured()
    {
        var message = "[yellow]LLM 接続が未設定です。Llm:Endpoint / Llm:ApiKey / Llm:Model を設定してください。[/]";
        AnsiConsole.MarkupLine(message);
    }
}
