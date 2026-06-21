namespace PcAgent.Diagnostics;

using System.Diagnostics;

// 診断処理のトレース用 ActivitySource。OTLP 送信は Agent 側の TracerProvider が
// この名前を購読することで行われる(本プロジェクトは OTel 依存を持たない)。
public static class DiagnosticsTelemetry
{
    // 診断スパンの ActivitySource 名。
    public const string SourceName = "PcAgent.Diagnostics";

    // 診断スパンを発行する ActivitySource。
    public static ActivitySource Source { get; } = new(SourceName);
}
