namespace PcAgent.Tui.Rendering;

using System.Globalization;
using System.Text;

using PcAgent.Diagnostics.Models;

// CollectorResult をプレーンテキストへ整形する(リッチ表示はフェーズ4で Spectre 化)。
internal static class InfoRenderer
{
    public static string Render(IReadOnlyList<CollectorResult> results)
    {
        var builder = new StringBuilder();
        foreach (var result in results)
        {
            builder.Append("# ").AppendLine(result.DisplayName);
            if (result.Note is { Length: > 0 })
            {
                builder.Append("  ! ").AppendLine(result.Note);
            }

            foreach (var group in result.Groups)
            {
                builder.Append("  ").AppendLine(group.Name);
                if (group.Values.Count == 0)
                {
                    builder.AppendLine("    (no data)");
                    continue;
                }

                foreach (var value in group.Values)
                {
                    builder.Append("    ").Append(value.Name).Append(": ").AppendLine(Format(value));
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Format(MetricValue value)
    {
        if (value.Text is { Length: > 0 })
        {
            return value.Text;
        }

        if (value.Value is not { } number)
        {
            return "N/A";
        }

        var text = number.ToString("0.##", CultureInfo.InvariantCulture);
        return value.Unit is { Length: > 0 } unit ? $"{text} {unit}" : text;
    }
}
