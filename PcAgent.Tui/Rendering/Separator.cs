namespace PcAgent.Tui.Rendering;

using Spectre.Console;

// 区切り線。「─」(box-drawing) を用いる。単独行のため他の内容と桁が揃う必要がなく、
// テーブル枠のような桁ずれの実害は生じない(線自体の幅だけが端末の曖昧幅設定に依存)。
internal static class Separator
{
    public const int DefaultWidth = 64;

    public static void Line(int width = DefaultWidth, int indent = 0, string color = "grey50")
    {
        var pad = indent > 0 ? new string(' ', indent) : String.Empty;
        var rule = new string('─', Math.Max(1, width));
        AnsiConsole.MarkupLine($"{pad}[{color}]{rule}[/]");
    }
}
