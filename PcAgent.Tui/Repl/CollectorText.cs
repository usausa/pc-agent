namespace PcAgent.Tui.Repl;

using System.Globalization;
using System.Text;

using PcAgent.Diagnostics.Models;

// CollectorResult を、エージェントへ注入する用のプレーンテキストへ整形する。
internal static class CollectorText
{
    public static string ToText(CollectorResult result)
    {
        var builder = new StringBuilder();
        if (result.Note is { Length: > 0 })
        {
            builder.AppendLine(result.Note);
        }

        foreach (var group in result.Groups)
        {
            builder.Append('[').Append(group.Name).Append(']').AppendLine();
            foreach (var value in group.Values)
            {
                builder.Append(value.Name).Append(": ").AppendLine(Format(value));
            }
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
