namespace PcAgent.Tui.Rendering;

using Spectre.Console;

// ハイフンのみの区切り線。桁ずれが生じうる box-drawing 罫線の代わりに使う(ハイフンは等幅でずれない)。
internal static class Separator
{
    public const int DefaultWidth = 64;

    public static void Line(int width = DefaultWidth, int indent = 0, string color = "grey50")
    {
        var pad = indent > 0 ? new string(' ', indent) : String.Empty;
        var dashes = new string('-', Math.Max(1, width));
        AnsiConsole.MarkupLine($"{pad}[{color}]{dashes}[/]");
    }
}
