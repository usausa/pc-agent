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

        var hint = "[silver]/help でコマンド一覧。@<category> で情報取得、!<command> でシェル、その他はエージェントへ。/exit で終了。[/]";
        AnsiConsole.MarkupLine(hint);

        AnsiConsole.WriteLine();
    }
}
