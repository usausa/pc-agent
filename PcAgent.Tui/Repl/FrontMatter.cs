namespace PcAgent.Tui.Repl;

// Markdown 先頭の frontmatter(--- で囲まれた name/description/argument-hint)を簡易解析する。
internal static class FrontMatter
{
    // 解析結果。Name が空ならファイル名で補完する。
    public readonly record struct Document(string Name, string Description, string? ArgumentHint, string Body);

    public static Document? Parse(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            // frontmatter 無し: 本文のみ(名前は呼び出し側でファイル名から補完)。
            return new Document(String.Empty, String.Empty, null, normalized.Trim());
        }

        var end = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var header = normalized[4..end];
        var bodyStart = normalized.IndexOf('\n', end + 1);
        var body = bodyStart < 0 ? String.Empty : normalized[(bodyStart + 1)..].Trim();

        var name = String.Empty;
        var description = String.Empty;
        string? argumentHint = null;

        foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            switch (key)
            {
                case "name":
                    name = value;
                    break;
                case "description":
                    description = value;
                    break;
                case "argument-hint":
                    argumentHint = value;
                    break;
                default:
                    break;
            }
        }

        return new Document(name, description, argumentHint, body);
    }
}
