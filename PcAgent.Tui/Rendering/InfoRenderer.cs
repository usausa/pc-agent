namespace PcAgent.Tui.Rendering;

using System.Globalization;

using PcAgent.Diagnostics.Models;

using Spectre.Console;

// CollectorResult を Spectre.Console でリッチ表示する。罫線は使わず、色 + 絵文字 + 進捗バーで表現。
internal static class InfoRenderer
{
    public static void Render(IReadOnlyList<CollectorResult> results)
    {
        foreach (var result in results)
        {
            var header = $"[bold aqua]{IconFor(result.Collector)} {Markup.Escape(result.DisplayName)}[/]";
            AnsiConsole.MarkupLine(header);

            if (result.Note is { Length: > 0 })
            {
                var note = $"[yellow]:warning:  {Markup.Escape(result.Note)}[/]";
                AnsiConsole.MarkupLine(note);
            }

            foreach (var group in result.Groups)
            {
                var groupLine = $"  [blue]{Markup.Escape(group.Name)}[/]";
                AnsiConsole.MarkupLine(groupLine);

                if (group.Values.Count == 0)
                {
                    var empty = "    [silver](no data)[/]";
                    AnsiConsole.MarkupLine(empty);
                    continue;
                }

                foreach (var value in group.Values)
                {
                    AnsiConsole.MarkupLine(FormatLine(value));
                }
            }

            AnsiConsole.WriteLine();
        }
    }

    private static string FormatLine(MetricValue value)
    {
        var name = Markup.Escape(value.Name);

        if (value.Text is { Length: > 0 })
        {
            return $"    [white]{name}[/]: [silver]{Markup.Escape(value.Text)}[/]";
        }

        if (value.Value is not { } number)
        {
            return $"    [white]{name}[/]: [silver]N/A[/]";
        }

        var text = number.ToString("0.##", CultureInfo.InvariantCulture);
        var unit = value.Unit is { Length: > 0 } u ? " " + u : String.Empty;
        var valueMarkup = $"[aqua]{text}{Markup.Escape(unit)}[/]";

        if (value.Unit == "%")
        {
            valueMarkup += " " + Bar(number);
        }

        return $"    [white]{name}[/]: {valueMarkup}";
    }

    private static string Bar(double percent)
    {
        var clamped = Math.Clamp(percent, 0.0, 100.0);
        var filled = (int)Math.Round(clamped / 10.0, MidpointRounding.AwayFromZero);
        var color = clamped >= 90.0 ? "red" : clamped >= 70.0 ? "yellow" : "green";
        return $"[{color}]{new string('█', filled)}{new string('░', 10 - filled)}[/]";
    }

    private static string IconFor(string collector) => collector switch
    {
        "cpu" => ":gear:",
        "gpu" => ":video_game:",
        "memory" => ":bar_chart:",
        "motherboard" => ":puzzle_piece:",
        "disk" => ":floppy_disk:",
        "network" => ":globe_showing_americas:",
        "battery" => ":battery:",
        "smart" => ":stethoscope:",
        "system" => ":desktop_computer:",
        _ => ":small_blue_diamond:",
    };
}
