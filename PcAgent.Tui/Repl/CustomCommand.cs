namespace PcAgent.Tui.Repl;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using PcAgent.Agent;
using PcAgent.Diagnostics.Collectors;
using PcAgent.Tui.Rendering;

using Spectre.Console;

// Markdown(frontmatter + 本文)から生成されるカスタム /コマンド。
// 本文中の $ARGUMENTS/$1.. ・@collector ・!`cmd` を展開し、エージェントへ送る。
public sealed partial class CustomCommand : ISlashCommand
{
    private readonly string body;
    private readonly IReadOnlyList<ICollector> collectors;
    private readonly ShellRunner shell;
    private readonly IAgentConversation conversation;

    public CustomCommand(
        string name,
        string description,
        string? argumentHint,
        string body,
        IReadOnlyList<ICollector> collectors,
        ShellRunner shell,
        IAgentConversation conversation)
    {
        Name = name;
        Description = description;
        ArgumentHint = argumentHint;
        this.body = body;
        this.collectors = collectors;
        this.shell = shell;
        this.conversation = conversation;
    }

    public string Name { get; }

    public string Description { get; }

    public string? ArgumentHint { get; }

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var prompt = await ExpandAsync(arguments, cancellationToken);

        if (!conversation.IsConfigured)
        {
            var note = "[yellow]LLM 接続が未設定です。展開結果のみ表示します:[/]";
            AnsiConsole.MarkupLine(note);
            AnsiConsole.Write(new Text(prompt));
            AnsiConsole.WriteLine();
            return;
        }

        var youLine = $"[bold green]> you[/]  [silver]/{Markup.Escape(Name)} {Markup.Escape(arguments)}[/]";
        AnsiConsole.MarkupLine(youLine);
        await ConversationRenderer.StreamAsync(conversation, prompt, cancellationToken);
    }

    private async Task<string> ExpandAsync(string arguments, CancellationToken cancellationToken)
    {
        var text = body.Replace("$ARGUMENTS", arguments, StringComparison.Ordinal);

        var positional = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < positional.Length; i++)
        {
            var token = "$" + (i + 1).ToString(CultureInfo.InvariantCulture);
            text = text.Replace(token, positional[i], StringComparison.Ordinal);
        }

        text = await ExpandCollectorsAsync(text, cancellationToken);
        text = await ExpandShellAsync(text, cancellationToken);
        return text;
    }

    private async Task<string> ExpandCollectorsAsync(string text, CancellationToken cancellationToken)
    {
        var tokens = CollectorToken().Matches(text).Select(static m => m.Value).Distinct(StringComparer.Ordinal).ToList();
        if (tokens.Count == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text);
        foreach (var token in tokens)
        {
            var name = token[1..];
            var collector = collectors.FirstOrDefault(c => String.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (collector is null)
            {
                continue;
            }

            var result = await collector.CollectAsync(cancellationToken);
            builder.Replace(token, "\n[参考情報: " + collector.DisplayName + "]\n" + CollectorText.ToText(result));
        }

        return builder.ToString();
    }

    private async Task<string> ExpandShellAsync(string text, CancellationToken cancellationToken)
    {
        var tokens = ShellToken().Matches(text).Select(static m => m.Value).Distinct(StringComparer.Ordinal).ToList();
        if (tokens.Count == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text);
        foreach (var token in tokens)
        {
            var command = token[2..^1];
            var output = await shell.CaptureAsync(command, cancellationToken);
            builder.Replace(token, "\n[$ " + command + "]\n" + output);
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"@([A-Za-z][A-Za-z0-9_.\-]*)")]
    private static partial Regex CollectorToken();

    [GeneratedRegex(@"!`([^`]+)`")]
    private static partial Regex ShellToken();
}
