namespace PcAgent.Diagnostics;

using System.Diagnostics.Metrics;

// 診断のメトリクス。OTLP 送信は Agent 側の MeterProvider がこの Meter を購読することで行われる
// (本プロジェクトは OTel 依存を持たない)。
public static class DiagnosticsMetrics
{
    // メトリクスの Meter 名。MeterProvider で AddMeter する。
    public const string MeterName = "PcAgent.Diagnostics";

    private static readonly Meter PcAgentMeter = new(MeterName);

    // 診断の実行回数。
    public static Counter<long> Diagnoses { get; } =
        PcAgentMeter.CreateCounter<long>("pcagent.diagnoses.count", description: "診断の実行回数");

    // 重大度別の指摘件数(severity 次元)。
    public static Counter<long> Findings { get; } =
        PcAgentMeter.CreateCounter<long>("pcagent.findings.count", description: "重大度別の指摘件数");

    // コレクタの収集回数。
    public static Counter<long> Collections { get; } =
        PcAgentMeter.CreateCounter<long>("pcagent.collections.count", description: "収集回数");

    // スナップショット構築の所要時間(ms)。
    public static Histogram<double> SnapshotDuration { get; } =
        PcAgentMeter.CreateHistogram<double>("pcagent.snapshot.duration", unit: "ms", description: "スナップショット構築の所要時間");
}
