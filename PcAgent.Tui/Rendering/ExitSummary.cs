namespace PcAgent.Tui.Rendering;

using System.Globalization;

using Spectre.Console;

// REPL 終了時のセッション概要(入力件数・指摘件数・所要時間)を表示する。罫線は使わない。
internal static class ExitSummary
{
    public static void Show(int inputs, int findings, TimeSpan elapsed)
    {
        var time = elapsed.TotalMinutes >= 1
            ? String.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalMinutes:0.0} 分")
            : String.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalSeconds:0} 秒");

        var problems = findings > 0
            ? String.Create(CultureInfo.InvariantCulture, $"[yellow]{findings}[/]")
            : "[green]0[/]";

        AnsiConsole.WriteLine();
        Separator.Line(48);
        var line = $"📋 [silver]セッション概要[/]  入力 [aqua]{inputs.ToString(CultureInfo.InvariantCulture)}[/] 件 / 指摘 {problems} 件 / 経過 [aqua]{time}[/]";
        AnsiConsole.MarkupLine(line);
    }
}
