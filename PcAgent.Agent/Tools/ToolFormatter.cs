namespace PcAgent.Agent.Tools;

using System.Globalization;
using System.Text;

using PcAgent.Diagnostics.Models;

// 収集結果を LLM 向けの読みやすいテキストへ整形する。
internal static class ToolFormatter
{
    public static string Format(CollectorResult result)
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
                builder.Append(value.Name).Append(": ").AppendLine(FormatValue(value));
            }
        }

        return builder.ToString();
    }

    private static string FormatValue(MetricValue value)
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
