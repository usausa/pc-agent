namespace PcAgent.Diagnostics.Models;

// 1つの測定値。Text があれば文字列、無ければ Value + Unit を表示する。
public sealed record MetricValue(string Name, double? Value, string? Unit, string? Text);

// コンポーネント/デバイス単位の測定値グループ。
public sealed record MetricGroup(string Name, IReadOnlyList<MetricValue> Values);

// 1つのコレクターの収集結果。
public sealed record CollectorResult(string Collector, string DisplayName, IReadOnlyList<MetricGroup> Groups, string? Note);
