namespace PcAgent.Tui.Repl;

using PcAgent.Agent;
using PcAgent.Tui.Rendering;

using Spectre.Console;

// /help : コマンド一覧。
public sealed class HelpCommand : ISlashCommand
{
    public string Name => "help";

    public string Description => "コマンド一覧を表示";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var header = "[bold aqua]commands[/]";
        AnsiConsole.MarkupLine(header);

        foreach (var command in context.Commands)
        {
            var hint = command.ArgumentHint is { Length: > 0 } h ? " " + h : String.Empty;
            var line = $"  [green]/{Markup.Escape(command.Name)}[/][silver]{Markup.Escape(hint)}[/]  [silver]-[/]  {Markup.Escape(command.Description)}";
            AnsiConsole.MarkupLine(line);
        }

        var at = "  [green]@<category>[/] [silver][[question]][/]  [silver]-[/]  情報取得(質問を続けるとエージェントへ注入)";
        AnsiConsole.MarkupLine(at);
        var bang = "  [green]![/][silver]<command>[/]  [silver]-[/]  シェル実行";
        AnsiConsole.MarkupLine(bang);
        var natural = "  [silver]その他の入力はエージェントへの質問になります。[/]";
        AnsiConsole.MarkupLine(natural);

        return ValueTask.CompletedTask;
    }
}

// /clear : 画面クリア。
public sealed class ClearCommand : ISlashCommand
{
    public string Name => "clear";

    public string Description => "画面をクリア";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        Banner.Show();
        return ValueTask.CompletedTask;
    }
}

// /exit : 終了。
public sealed class ExitCommand : ISlashCommand
{
    public string Name => "exit";

    public string Description => "終了";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        context.ExitRequested = true;
        return ValueTask.CompletedTask;
    }
}

// /model : 使用モデル表示。
public sealed class ModelCommand(IAgentConversation conversation) : ISlashCommand
{
    public string Name => "model";

    public string Description => "使用モデルを表示";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var state = conversation.IsConfigured ? "[green]configured[/]" : "[yellow]not configured[/]";
        var line = $"[white]model[/]: [aqua]{Markup.Escape(conversation.ModelName)}[/]  ({state})";
        AnsiConsole.MarkupLine(line);
        var note = "[silver](モデル切替は将来対応)[/]";
        AnsiConsole.MarkupLine(note);
        return ValueTask.CompletedTask;
    }
}
