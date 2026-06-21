namespace PcAgent.Tui.Rendering;

using Spectre.Console;

// 起動スプラッシュ(ロゴ + タグライン + ヒント)。罫線は使わない。
internal static class Banner
{
    public static void Show()
    {
        AnsiConsole.Write(new FigletText("PC Agent").Color(Color.Aqua));

        var tagline = "[silver]Windows PC diagnostics & information agent[/]";
        AnsiConsole.MarkupLine(tagline);

        var hint = "[silver]対話モードはフェーズ5で実装予定。--ask \"<質問>\" で単発質問、--help でコマンド一覧。[/]";
        AnsiConsole.MarkupLine(hint);

        AnsiConsole.WriteLine();
    }
}
