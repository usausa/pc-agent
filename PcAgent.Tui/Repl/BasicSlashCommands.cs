namespace PcAgent.Tui.Repl;

using System.Globalization;

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

// /compact : 会話履歴をクリアして文脈を解放(自動圧縮とは別の手動全クリア)。
public sealed class CompactCommand(IAgentConversation conversation) : ISlashCommand
{
    public string Name => "compact";

    public string Description => "会話履歴をクリアして文脈を解放";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        if (!conversation.IsConfigured)
        {
            AnsiConsole.MarkupLine("[yellow]LLM 未設定のため、保持している会話履歴はありません。[/]");
            return ValueTask.CompletedTask;
        }

        conversation.ResetConversation();
        AnsiConsole.MarkupLine("[silver]会話履歴をクリアしました(文脈を解放)。[/]");
        return ValueTask.CompletedTask;
    }
}

// /context : コンテキスト使用状況を表示(モデル・使用率・圧縮・ツール内訳など)。
public sealed class ContextCommand(IAgentConversation conversation) : ISlashCommand
{
    public string Name => "context";

    public string Description => "コンテキスト使用状況を表示";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        if (!conversation.IsConfigured)
        {
            AnsiConsole.MarkupLine("[yellow]LLM 未設定のため、コンテキスト情報はありません。[/]");
            return ValueTask.CompletedTask;
        }

        var info = conversation.GetContextInfo();
        var culture = CultureInfo.InvariantCulture;

        AnsiConsole.MarkupLine("[bold aqua]🧮 コンテキスト[/]");
        Separator.Line(52);

        AnsiConsole.MarkupLine($"  [white]model[/]      : [aqua]{Markup.Escape(info.ModelName)}[/]  [silver](window {info.ContextWindow.ToString("#,##0", culture)})[/]");

        if (info.HasUsage)
        {
            var percent = info.ContextWindow > 0 ? Math.Clamp((double)info.LastInputTokens / info.ContextWindow * 100.0, 0.0, 100.0) : 0.0;
            var filled = (int)Math.Round(percent / 10.0, MidpointRounding.AwayFromZero);
            var color = percent >= 90.0 ? "red" : percent >= 70.0 ? "yellow" : "green";
            var bar = $"[{color}]{new string('█', filled)}{new string('░', 10 - filled)}[/]";
            AnsiConsole.MarkupLine($"  [white]used[/]       : [aqua]{info.LastInputTokens.ToString("#,##0", culture)}[/] / {info.ContextWindow.ToString("#,##0", culture)}  ({percent.ToString("0.#", culture)}%)  {bar}");
            AnsiConsole.MarkupLine($"  [white]last turn[/]  : in [aqua]{info.LastInputTokens.ToString("#,##0", culture)}[/] / out [aqua]{info.LastOutputTokens.ToString("#,##0", culture)}[/]");
            AnsiConsole.MarkupLine($"  [white]session[/]    : [aqua]{info.TurnCount.ToString(culture)}[/] turns / out 計 [aqua]{info.TotalOutputTokens.ToString("#,##0", culture)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [white]used[/]       : [silver]未計測(送信後に表示)[/]  ([aqua]{info.TurnCount.ToString(culture)}[/] turns)");
        }

        var compaction = info.CompactionEnabled
            ? $"[green]有効[/]  {info.CompactionThreshold.ToString(culture)} msgs 超で圧縮 / 直近 {info.CompactionKeepTurns.ToString(culture)} ターン保持"
            : "[silver]無効[/]";
        AnsiConsole.MarkupLine($"  [white]compaction[/] : {compaction}");

        var tools = info.Tools.Count > 0 ? Markup.Escape(String.Join(", ", info.Tools)) : "(なし)";
        AnsiConsole.MarkupLine($"  [white]tools[/] ([aqua]{info.Tools.Count.ToString(culture)}[/]) : [#b1b9f9]{tools}[/]");

        var rag = info.RagEnabled ? $"on ([aqua]{info.RagDocCount.ToString(culture)}[/] docs)" : "off";
        AnsiConsole.MarkupLine($"  [white]rag[/]        : {rag}");
        AnsiConsole.MarkupLine($"  [white]providers[/]  : [aqua]{info.ProviderCount.ToString(culture)}[/]");

        return ValueTask.CompletedTask;
    }
}
