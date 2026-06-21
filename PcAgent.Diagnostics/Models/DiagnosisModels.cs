namespace PcAgent.Diagnostics.Models;

// 重大度。値の大小がそのまま深刻度の順序になる。
public enum Severity
{
    Ok,
    Info,
    Warning,
    Critical,
}

// 診断対象の 1 メトリクス(パス・値・出所)。
public readonly record struct SnapshotMetric(string Path, double Value, string Source);

// 診断用スナップショット(全メトリクス)。
public sealed record DiagnosticsSnapshot(IReadOnlyList<SnapshotMetric> Metrics, DateTimeOffset Timestamp);

// 1 つの指摘。
public sealed record Finding(string RuleId, string Metric, Severity Severity, string Message, string Source, double Actual, double Threshold, string? Action);

// 診断レポート(ルールが決定的に生成)。
public sealed record DiagnosisReport(DateTimeOffset Timestamp, Severity Overall, IReadOnlyList<Finding> Findings, IReadOnlyList<string> Actions);
