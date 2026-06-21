namespace PcAgent.Tui.Rendering;

using PcAgent.Diagnostics.Collectors;
using PcAgent.Diagnostics.Models;
using PcAgent.Diagnostics.Platform;

using Spectre.Console;

// info / @ で共有する「カテゴリ解決 → 収集 → リッチ描画」。
internal static class InfoExecutor
{
    // null=不明カテゴリ / 空=未指定 / それ以外=対象コレクタ。
    public static List<ICollector>? ResolveTargets(IReadOnlyList<ICollector> available, string? category)
    {
        var trimmed = category?.Trim();
        if (String.IsNullOrEmpty(trimmed))
        {
            return [];
        }

        if (String.Equals(trimmed, "all", StringComparison.OrdinalIgnoreCase))
        {
            return [.. available];
        }

        var match = available.FirstOrDefault(c => String.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : [match];
    }

    public static async Task RunAsync(IEnumerable<ICollector> collectors, string? category, CancellationToken cancellationToken)
    {
        var available = collectors.OrderBy(static c => c.Name, StringComparer.Ordinal).ToList();
        var targets = ResolveTargets(available, category);
        var names = String.Join(", ", available.Select(static c => c.Name));

        if (targets is null)
        {
            var unknown = $"[yellow]Unknown category '{Markup.Escape(category ?? String.Empty)}'. Available: {Markup.Escape(names)}, all[/]";
            AnsiConsole.MarkupLine(unknown);
            return;
        }

        if (targets.Count == 0)
        {
            var hint = $"[silver]Available: {Markup.Escape(names)}, all[/]";
            AnsiConsole.MarkupLine(hint);
            return;
        }

        if (!AdminChecker.IsAdministrator())
        {
            var warn = "[yellow]:warning:  管理者権限がないため、一部のハードウェア情報が取得できない場合があります。[/]";
            AnsiConsole.MarkupLine(warn);
        }

        var results = new List<CollectorResult>();
        foreach (var collector in targets)
        {
            results.Add(await collector.CollectAsync(cancellationToken));
        }

        InfoRenderer.Render(results);
    }
}
