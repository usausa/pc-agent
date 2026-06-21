namespace PcAgent.Tui.Rendering;

using System.Globalization;

using PcAgent.Diagnostics.Models;

using Spectre.Console;

// 診断レポートを Spectre で表示する(罫線なし・絵文字ステータス)。
internal static class DiagnosisRenderer
{
    public static void Render(DiagnosisReport report)
    {
        var count = report.Findings.Count.ToString(CultureInfo.InvariantCulture);
        var overall = $"[bold]{Icon(report.Overall)} 診断結果: {Text(report.Overall)}[/]  [silver]({count} 件)[/]";
        AnsiConsole.MarkupLine(overall);

        if (report.Findings.Count == 0)
        {
            var ok = "  [green]:check_mark_button: 問題は見つかりませんでした[/]";
            AnsiConsole.MarkupLine(ok);
        }

        foreach (var finding in report.Findings)
        {
            AnsiConsole.MarkupLine(FormatFinding(finding));
        }

        if (report.Actions.Count > 0)
        {
            var head = "[silver]推奨アクション:[/]";
            AnsiConsole.MarkupLine(head);
            foreach (var action in report.Actions)
            {
                var line = $"  [silver]- {Markup.Escape(action)}[/]";
                AnsiConsole.MarkupLine(line);
            }
        }
    }

    public static void RenderSummary(DiagnosisReport report)
    {
        var crit = report.Findings.Count(static f => f.Severity == Severity.Critical).ToString(CultureInfo.InvariantCulture);
        var warn = report.Findings.Count(static f => f.Severity == Severity.Warning).ToString(CultureInfo.InvariantCulture);
        var info = report.Findings.Count(static f => f.Severity == Severity.Info).ToString(CultureInfo.InvariantCulture);
        var line = $"{Icon(report.Overall)} [bold]{Text(report.Overall)}[/]  [red]{crit} crit[/] / [yellow]{warn} warn[/] / [blue]{info} info[/]";
        AnsiConsole.MarkupLine(line);
    }

    private static string FormatFinding(Finding finding)
    {
        var actual = finding.Actual.ToString("0.##", CultureInfo.InvariantCulture);
        var threshold = finding.Threshold.ToString("0.##", CultureInfo.InvariantCulture);
        var detail = $"{finding.Metric} = {actual}, 閾値 {threshold}";
        return $"  {Icon(finding.Severity)} [white]{Markup.Escape(finding.Source)}[/]  [silver][[{Markup.Escape(detail)}]][/]  {Markup.Escape(finding.Message)}";
    }

    private static string Icon(Severity severity) => severity switch
    {
        Severity.Critical => "[red]:cross_mark:[/]",
        Severity.Warning => "[yellow]:warning:[/]",
        Severity.Info => "[blue]:information:[/]",
        _ => "[green]:check_mark_button:[/]",
    };

    private static string Text(Severity severity) => severity switch
    {
        Severity.Critical => "Critical",
        Severity.Warning => "Warning",
        Severity.Info => "Info",
        _ => "OK",
    };
}
