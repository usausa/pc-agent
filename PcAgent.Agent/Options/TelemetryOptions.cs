namespace PcAgent.Agent.Options;

// OTLP エクスポートに関する設定。
public sealed class OtlpOptions
{
    // OTLP 送信を有効にするか。
    public bool Enabled { get; set; }

    // 送信先エンドポイント。
    public string Endpoint { get; set; } = "http://localhost:4317";
}

// 可観測性(テレメトリ)に関する設定。
public sealed class TelemetryOptions
{
    // 設定セクション名。
    public const string SectionName = "Telemetry";

    // プロンプト・応答をトレースに含めるか。
    public bool EnableSensitiveData { get; set; }

    // OTLP エクスポート設定。
    public OtlpOptions Otlp { get; set; } = new();
}
