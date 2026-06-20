namespace PcAgent.Tui.Commands;

using System.Text.Json;

using PcAgent.Diagnostics.Collectors;
using PcAgent.Diagnostics.Models;
using PcAgent.Diagnostics.Platform;
using PcAgent.Tui.Rendering;

using Smart.CommandLine.Hosting;

// 指定カテゴリの生情報を表示する。
[Command("info", "Show raw PC information for a category")]
public sealed class InfoCommand(IEnumerable<ICollector> collectors) : ICommandHandler
{
    // 情報カテゴリ。
    [Option<string>("--category", "-c", Description = "Category: cpu, gpu, memory, motherboard, disk, network, battery, smart, system, all")]
    public string? Category { get; set; }

    // JSON で出力するか。
    [Option("--json", "-j", Description = "Output as JSON")]
    public bool Json { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var available = collectors.OrderBy(static c => c.Name, StringComparer.Ordinal).ToList();
        var category = Category?.Trim();

        if (String.IsNullOrEmpty(category))
        {
            var hint = $"Specify a category with -c <name>. Available: {String.Join(", ", available.Select(static c => c.Name))}, all";
            await Console.Out.WriteLineAsync(hint);
            return;
        }

        List<ICollector> targets;
        if (String.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            targets = available;
        }
        else
        {
            var match = available.FirstOrDefault(c => String.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                var error = $"Unknown category '{category}'. Available: {String.Join(", ", available.Select(static c => c.Name))}, all";
                await Console.Error.WriteLineAsync(error);
                context.ExitCode = 1;
                return;
            }

            targets = [match];
        }

        if (!AdminChecker.IsAdministrator())
        {
            var warning = "管理者権限がないため、一部のハードウェア情報が取得できない場合があります。";
            await Console.Error.WriteLineAsync(warning);
        }

        var results = new List<CollectorResult>();
        foreach (var collector in targets)
        {
            var result = await collector.CollectAsync(context.CancellationToken);
            results.Add(result);
        }

        if (Json)
        {
            var json = JsonSerializer.Serialize(results, CollectorJsonContext.Default.ListCollectorResult);
            await Console.Out.WriteLineAsync(json);
        }
        else
        {
            await Console.Out.WriteAsync(InfoRenderer.Render(results));
        }
    }
}
