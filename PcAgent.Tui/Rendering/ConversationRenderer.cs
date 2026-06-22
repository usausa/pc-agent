namespace PcAgent.Tui.Rendering;

using System.Text;
using System.Text.RegularExpressions;

using PcAgent.Agent;

using Spectre.Console;

// エージェントの応答ストリーム(AgentEvent)を Spectre でスクロールバックへ逐次描画する。
// 「●」で処理内容、コマンド相当は淡色、応答末尾に「✅」。本文は行単位で簡易マークダウン整形する
// (### 見出し / - 箇条書き / **強調** は色に置換)。整形には行の文脈が要るため行バッファで描画する。
internal static partial class ConversationRenderer
{
    // ツール名・コマンド/インラインコード相当の淡い色。
    private const string CommandColor = "#b1b9f9";

    public static async Task<string> StreamAsync(IAgentConversation conversation, string message, CancellationToken cancellationToken)
    {
        var agentShown = false;
        var buffer = new StringBuilder();
        var full = new StringBuilder();

        await foreach (var agentEvent in conversation.SendAsync(message, cancellationToken))
        {
            switch (agentEvent)
            {
                case ToolCallStarted started:
                    if (agentShown)
                    {
                        FlushRemainder(buffer);
                        agentShown = false;
                    }

                    var args = String.IsNullOrEmpty(started.Arguments)
                        ? String.Empty
                        : $"  [grey50]{Markup.Escape(started.Arguments)}[/]";
                    AnsiConsole.MarkupLine($"[blue]●[/] [{CommandColor}]{Markup.Escape(started.Name)}[/]{args}");
                    break;

                case TextDelta delta:
                    if (!agentShown)
                    {
                        AnsiConsole.Markup("[aqua]●[/] [bold aqua]agent[/]  ");
                        agentShown = true;
                    }

                    buffer.Append(delta.Text);
                    full.Append(delta.Text);
                    FlushCompleteLines(buffer);
                    break;

                case ResponseCompleted:
                    if (agentShown)
                    {
                        FlushRemainder(buffer);
                        AnsiConsole.MarkupLine("  ✅");
                        agentShown = false;
                    }

                    AnsiConsole.WriteLine();
                    break;

                default:
                    break;
            }
        }

        return full.ToString();
    }

    // 改行で完結した行を整形して出力し、末尾の未完行はバッファに残す。
    private static void FlushCompleteLines(StringBuilder buffer)
    {
        var text = buffer.ToString();
        var start = 0;
        int newline;
        while ((newline = text.IndexOf('\n', start)) >= 0)
        {
            var line = text[start..newline].TrimEnd('\r');
            AnsiConsole.MarkupLine(FormatLine(line));
            start = newline + 1;
        }

        buffer.Clear();
        if (start < text.Length)
        {
            buffer.Append(text[start..]);
        }
    }

    // バッファに残った未完行(改行で終わらない最終行)を出力する。
    private static void FlushRemainder(StringBuilder buffer)
    {
        if (buffer.Length > 0)
        {
            AnsiConsole.MarkupLine(FormatLine(buffer.ToString().TrimEnd('\r')));
            buffer.Clear();
        }
    }

    // 1 行を簡易マークダウン整形して Spectre マークアップ文字列にする。
    private static string FormatLine(string line)
    {
        if (line.Length == 0)
        {
            return String.Empty;
        }

        var heading = HeadingPattern().Match(line);
        if (heading.Success)
        {
            // ### 等は章題を表す絵文字 + 色に置換する。
            return $"🔖 [bold deepskyblue1]{ApplyInline(heading.Groups[1].Value)}[/]";
        }

        var bullet = BulletPattern().Match(line);
        if (bullet.Success)
        {
            // 箇条書きは暖色(補色)のマーカーで青系に偏らせない。
            return $"{bullet.Groups[1].Value}[orange1]-[/] [white]{ApplyInline(bullet.Groups[2].Value)}[/]";
        }

        return $"[white]{ApplyInline(line)}[/]";
    }

    // インライン整形: **強調** → 色, `code` → コマンド色(両方とも記号を除去)。
    private static string ApplyInline(string text)
    {
        var escaped = Markup.Escape(text);
        escaped = BoldPattern().Replace(escaped, "[gold1]$1[/]");
        escaped = CodePattern().Replace(escaped, $"[{CommandColor}]$1[/]");
        return escaped;
    }

    [GeneratedRegex(@"^#{1,6}\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(\s*)-\s+(.*)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex CodePattern();
}
