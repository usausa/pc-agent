namespace PcAgent.Tui.Repl;

using System.Globalization;

using PcAgent.Diagnostics.Collectors;
using PcAgent.Diagnostics.Models;
using PcAgent.Tui.Rendering;

using Spectre.Console;

// /watch : 指定カテゴリの数値メトリクスをライブ監視する(何かキーを押すと終了)。
public sealed class WatchCommand(IEnumerable<ICollector> collectors) : ISlashCommand
{
    private const int MaxRows = 16;

    public string Name => "watch";

    public string Description => "メトリクスをライブ監視(キーで終了)";

    public string? ArgumentHint => "<category>";

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var available = collectors.OrderBy(static c => c.Name, StringComparer.Ordinal).ToList();
        var collector = available.FirstOrDefault(c => String.Equals(c.Name, arguments.Trim(), StringComparison.OrdinalIgnoreCase));
        if (collector is null)
        {
            var names = String.Join(", ", available.Select(static c => c.Name));
            AnsiConsole.MarkupLine($"[yellow]使い方: /watch <category>  ({Markup.Escape(names)})[/]");
            return;
        }

        // ライブ表示はインタラクティブ端末のみ。リダイレクト時は 1 回収集して表示。
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            InfoRenderer.Render([await collector.CollectAsync(cancellationToken)]);
            return;
        }

        AnsiConsole.MarkupLine($"[silver]watching [aqua]{Markup.Escape(collector.Name)}[/] — 何かキーを押すと終了[/]");

        try
        {
            var seed = BuildGrid(await collector.CollectAsync(cancellationToken));
            await AnsiConsole.Live(seed)
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        for (var i = 0; i < 10; i++)
                        {
                            if (Console.KeyAvailable)
                            {
                                Console.ReadKey(intercept: true);
                                return;
                            }

                            await Task.Delay(100, cancellationToken);
                        }

                        ctx.UpdateTarget(BuildGrid(await collector.CollectAsync(cancellationToken)));
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C 等での終了は正常。
        }
    }

    private static Grid BuildGrid(CollectorResult result)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();

        var rows = 0;
        foreach (var group in result.Groups)
        {
            foreach (var value in group.Values)
            {
                if (rows >= MaxRows)
                {
                    break;
                }

                if (value.Value is not { } number)
                {
                    continue;
                }

                var unit = value.Unit is { Length: > 0 } u ? " " + u : String.Empty;
                var display = number.ToString("0.##", CultureInfo.InvariantCulture) + unit;
                var bar = value.Unit is "%" or "°C" ? Bar(number) : String.Empty;
                grid.AddRow($"[white]{Markup.Escape(value.Name)}[/]", $"[aqua]{Markup.Escape(display)}[/]", bar);
                rows++;
            }
        }

        if (rows == 0)
        {
            grid.AddRow("[silver](no numeric data)[/]", String.Empty, String.Empty);
        }

        return grid;
    }

    private static string Bar(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 100.0);
        var filled = (int)Math.Round(clamped / 10.0, MidpointRounding.AwayFromZero);
        var color = clamped >= 90.0 ? "red" : clamped >= 70.0 ? "yellow" : "green";
        return $"[{color}]{new string('█', filled)}{new string('░', 10 - filled)}[/]";
    }
}
