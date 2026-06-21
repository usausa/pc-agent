namespace PcAgent.Tui.Rendering;

using System.Text;

using Spectre.Console;

// 起動スプラッシュ。ANSI Shadow 風のブロックロゴ("PC AGENT")を青系グラデーションで描く。
// █ と box-drawing(╗╔╝╚═║)で立体・影を表現する。区切りは桁ずれのないハイフンを使う(box-drawing の Rule は使わない)。
internal static class Banner
{
    private const int GlyphHeight = 6;

    // 行ごとの色。上から下へ青のグラデーション(明 → 濃)。
    private static readonly string[] Palette =
    [
        "deepskyblue1", "deepskyblue1", "dodgerblue1", "dodgerblue1", "blue3", "blue3",
    ];

    // "PC AGENT" の各文字の ANSI Shadow 字形(高さ 6)。各文字内の行幅は揃える。
    private static readonly string[][] Glyphs =
    [
        ["██████╗ ", "██╔══██╗", "██████╔╝", "██╔═══╝ ", "██║     ", "╚═╝     "],                  // P
        [" ██████╗", "██╔════╝", "██║     ", "██║     ", "╚██████╗", " ╚═════╝"],                  // C
        ["   ", "   ", "   ", "   ", "   ", "   "],                                                // (space)
        [" █████╗ ", "██╔══██╗", "███████║", "██╔══██║", "██║  ██║", "╚═╝  ╚═╝"],                  // A
        [" ██████╗ ", "██╔════╝ ", "██║  ███╗", "██║   ██║", "╚██████╔╝", " ╚═════╝ "],            // G
        ["███████╗", "██╔════╝", "█████╗  ", "██╔══╝  ", "███████╗", "╚══════╝"],                  // E
        ["███╗   ██╗", "████╗  ██║", "██╔██╗ ██║", "██║╚██╗██║", "██║ ╚████║", "╚═╝  ╚═══╝"],      // N
        ["████████╗", "╚══██╔══╝", "   ██║   ", "   ██║   ", "   ██║   ", "   ╚═╝   "],            // T
    ];

    public static void Show()
    {
        AnsiConsole.WriteLine();

        var logo = BuildLogo();
        for (var i = 0; i < logo.Length; i++)
        {
            AnsiConsole.MarkupLine($"  [{Palette[i % Palette.Length]}]{Markup.Escape(logo[i])}[/]");
        }

        var tagline = "[grey70]Windows PC diagnostics & information agent[/]";
        AnsiConsole.MarkupLine($"  {tagline}");

        Separator.Line(logo[0].Length, indent: 2);

        var hint = "[grey70]/help でコマンド一覧。@<category> で情報取得、!<command> でシェル、その他はエージェントへ。/exit で終了。[/]";
        AnsiConsole.MarkupLine($"  {hint}");

        AnsiConsole.WriteLine();
    }

    // 各文字の字形を 1 行ずつ連結してロゴ全体(行配列)を作る。
    private static string[] BuildLogo()
    {
        var lines = new string[GlyphHeight];
        for (var r = 0; r < GlyphHeight; r++)
        {
            var builder = new StringBuilder();
            foreach (var glyph in Glyphs)
            {
                builder.Append(glyph[r]);
            }

            lines[r] = builder.ToString();
        }

        return lines;
    }
}
