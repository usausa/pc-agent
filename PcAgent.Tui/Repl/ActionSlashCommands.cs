namespace PcAgent.Tui.Repl;

using System.Globalization;

using Microsoft.Extensions.Options;

using PcAgent.Diagnostics.Actions;
using PcAgent.Diagnostics.Options;

using Spectre.Console;

// /actions : 実行可能なアクション一覧。
public sealed class ActionsCommand(IOptions<ActionsOptions> options) : ISlashCommand
{
    public string Name => "actions";

    public string Description => "実行可能なアクション一覧";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var enabled = o.Enabled ? "[green]有効[/]" : "[yellow]無効[/]";
        var approval = o.RequireApproval ? "必須" : "任意";
        var shell = o.AllowShell ? "許可" : "不可";

        var header = $"[white]アクション[/]: {enabled}  [silver](LLM承認: {approval} / シェル: {shell})[/]";
        AnsiConsole.MarkupLine(header);
        var l1 = "  [green]/clean temp[/]  [silver]-[/]  一時ファイルを削除(確認後)";
        AnsiConsole.MarkupLine(l1);
        var l2 = "  [green]/clean binobj <root>[/]  [silver]-[/]  指定ルート配下の bin/obj を削除(確認後)";
        AnsiConsole.MarkupLine(l2);
        return ValueTask.CompletedTask;
    }
}

// /clean : クリーンアップ(列挙 → 確認 → 削除)。
public sealed class CleanCommand(IOptions<ActionsOptions> options) : ISlashCommand
{
    public string Name => "clean";

    public string Description => "クリーンアップ(列挙→確認→削除)";

    public string? ArgumentHint => "temp | binobj <root>";

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            var disabled = "[yellow]アクションは無効です(Actions:Enabled=false)。[/]";
            AnsiConsole.MarkupLine(disabled);
            return ValueTask.CompletedTask;
        }

        var parts = arguments.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kind = parts.Length > 0 ? parts[0] : String.Empty;

        CleanupPlan plan;
        if (String.Equals(kind, "temp", StringComparison.OrdinalIgnoreCase))
        {
            plan = MaintenanceService.PlanTemp();
        }
        else if (String.Equals(kind, "binobj", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2)
            {
                var usage = "[yellow]使い方: /clean binobj <探索ルート>[/]";
                AnsiConsole.MarkupLine(usage);
                return ValueTask.CompletedTask;
            }

            plan = MaintenanceService.PlanBinObj([parts[1]]);
        }
        else
        {
            var usage = "[yellow]使い方: /clean temp  または  /clean binobj <root>[/]";
            AnsiConsole.MarkupLine(usage);
            return ValueTask.CompletedTask;
        }

        ShowPlan(plan);
        if (plan.Items.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        if (!Confirm())
        {
            var cancelled = "[silver]中止しました。[/]";
            AnsiConsole.MarkupLine(cancelled);
            return ValueTask.CompletedTask;
        }

        var result = MaintenanceService.Execute(plan);
        var done = $"[green]削除 {result.Deleted.ToString(CultureInfo.InvariantCulture)} 件 / 失敗 {result.Failed.ToString(CultureInfo.InvariantCulture)} 件 / 解放 {Bytes(result.BytesFreed)}[/]";
        AnsiConsole.MarkupLine(done);
        return ValueTask.CompletedTask;
    }

    private static void ShowPlan(CleanupPlan plan)
    {
        var count = plan.Items.Count.ToString(CultureInfo.InvariantCulture);
        var header = $"[white]対象 {count} 件 / 合計 {Bytes(plan.TotalBytes)}[/]";
        AnsiConsole.MarkupLine(header);

        foreach (var item in plan.Items.Take(15))
        {
            var line = $"  [#b1b9f9]{Markup.Escape(item.Path)}[/]  [silver]({Bytes(item.Bytes)})[/]";
            AnsiConsole.MarkupLine(line);
        }

        if (plan.Items.Count > 15)
        {
            var more = (plan.Items.Count - 15).ToString(CultureInfo.InvariantCulture);
            var line = $"  [silver]... 他 {more} 件[/]";
            AnsiConsole.MarkupLine(line);
        }
    }

    private static bool Confirm()
    {
        var prompt = "[bold yellow]削除しますか? (y/N): [/]";
        AnsiConsole.Markup(prompt);
        var answer = Console.ReadLine()?.Trim();
        return String.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) || String.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string Bytes(long bytes)
    {
        var megabytes = bytes / (1024.0 * 1024.0);
        return megabytes.ToString("0.##", CultureInfo.InvariantCulture) + " MB";
    }
}
