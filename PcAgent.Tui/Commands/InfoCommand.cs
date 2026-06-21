namespace PcAgent.Tui.Commands;

using System.Text.Json;

using PcAgent.Diagnostics.Collectors;
using PcAgent.Diagnostics.Models;
using PcAgent.Tui.Rendering;

using Smart.CommandLine.Hosting;

// 指定カテゴリの生情報を表示する(非対話の単発コマンド)。
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
        if (!Json)
        {
            await InfoExecutor.RunAsync(collectors, Category, context.CancellationToken);
            return;
        }

        var available = collectors.OrderBy(static c => c.Name, StringComparer.Ordinal).ToList();
        var targets = InfoExecutor.ResolveTargets(available, Category);
        var names = String.Join(", ", available.Select(static c => c.Name));

        if (targets is null)
        {
            var error = $"Unknown category '{Category}'. Available: {names}, all";
            await Console.Error.WriteLineAsync(error);
            context.ExitCode = 1;
            return;
        }

        if (targets.Count == 0)
        {
            var hint = $"Specify a category with -c <name>. Available: {names}, all";
            await Console.Out.WriteLineAsync(hint);
            return;
        }

        var results = new List<CollectorResult>();
        foreach (var collector in targets)
        {
            results.Add(await collector.CollectAsync(context.CancellationToken));
        }

        var json = JsonSerializer.Serialize(results, CollectorJsonContext.Default.ListCollectorResult);
        await Console.Out.WriteLineAsync(json);
    }
}
