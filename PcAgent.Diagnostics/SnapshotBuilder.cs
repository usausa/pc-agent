namespace PcAgent.Diagnostics;

using System.Diagnostics;

using PcAgent.Diagnostics.Collectors;
using PcAgent.Diagnostics.Models;

// 収集結果から診断用メトリクス(path → 値)のスナップショットを構築する。
public sealed class SnapshotBuilder(IEnumerable<ICollector> collectors)
{
    public async Task<DiagnosticsSnapshot> BuildAsync(CancellationToken cancellationToken)
    {
        using var activity = DiagnosticsTelemetry.Source.StartActivity("diagnostics.snapshot");
        var stopwatch = Stopwatch.StartNew();

        var metrics = new List<SnapshotMetric>();
        foreach (var collector in collectors)
        {
            var result = await collector.CollectAsync(cancellationToken).ConfigureAwait(false);
            DiagnosticsMetrics.Collections.Add(1);
            Extract(collector.Name, result, metrics);
        }

        activity?.SetTag("metric.count", metrics.Count);
        DiagnosticsMetrics.SnapshotDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        return new DiagnosticsSnapshot(metrics, DateTimeOffset.Now);
    }

    private static void Extract(string collector, CollectorResult result, List<SnapshotMetric> metrics)
    {
        switch (collector)
        {
            case "cpu":
                AddMaxByUnit(metrics, result, "cpu.temperature", "°C");
                break;
            case "gpu":
                AddMaxByUnit(metrics, result, "gpu.temperature", "°C");
                break;
            case "memory":
                AddMaxByUnit(metrics, result, "memory.loadPercent", "%");
                break;
            case "disk":
                AddPerGroup(metrics, result, "disk.temperature", "Temperature");
                break;
            case "smart":
                AddPerGroup(metrics, result, "smart.criticalWarning", "Critical Warning");
                AddPerGroup(metrics, result, "smart.mediaErrors", "Media Errors");
                AddPerGroup(metrics, result, "smart.percentageUsed", "Percentage Used");
                AddPerGroup(metrics, result, "smart.availableSpare", "Available Spare");
                break;
            case "battery":
                AddPerGroup(metrics, result, "battery.degradation", "Degradation Level");
                break;
            case "system":
                foreach (var group in result.Groups)
                {
                    if (group.Name.StartsWith("Drive ", StringComparison.Ordinal))
                    {
                        AddNamedFromGroup(metrics, group, "system.diskUsedPercent", "Used");
                    }
                }

                break;
            default:
                break;
        }
    }

    private static void AddPerGroup(List<SnapshotMetric> metrics, CollectorResult result, string path, string valueName)
    {
        foreach (var group in result.Groups)
        {
            AddNamedFromGroup(metrics, group, path, valueName);
        }
    }

    private static void AddNamedFromGroup(List<SnapshotMetric> metrics, MetricGroup group, string path, string valueName)
    {
        var value = group.Values.FirstOrDefault(v => String.Equals(v.Name, valueName, StringComparison.Ordinal));
        if (value?.Value is { } number)
        {
            metrics.Add(new SnapshotMetric(path, number, group.Name));
        }
    }

    private static void AddMaxByUnit(List<SnapshotMetric> metrics, CollectorResult result, string path, string unit)
    {
        double? max = null;
        var source = result.DisplayName;
        foreach (var group in result.Groups)
        {
            foreach (var value in group.Values)
            {
                if (String.Equals(value.Unit, unit, StringComparison.Ordinal) && value.Value is { } number && (max is null || number > max))
                {
                    max = number;
                    source = group.Name;
                }
            }
        }

        if (max is { } maxValue)
        {
            metrics.Add(new SnapshotMetric(path, maxValue, source));
        }
    }
}
