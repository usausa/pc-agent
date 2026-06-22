namespace PcAgent.Agent;

using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using PcAgent.Agent.Options;
using PcAgent.Diagnostics;

// 可観測性の初期化。OTLP 送信は Telemetry:Otlp:Enabled のときだけ有効化する(既定オフ)。
// 無効時はトレースの購読者が居ないため、スパン発行は実質ノーオペになる。
public sealed class AgentTelemetry : IDisposable
{
    // エージェント/ツールのスパンを発行する ActivitySource 名。UseOpenTelemetry と共有する。
    public const string SourceName = "PcAgent.Agent";

    private readonly TracerProvider? tracerProvider;

    private readonly MeterProvider? meterProvider;

    public AgentTelemetry(IOptions<TelemetryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var telemetry = options.Value;
        EnableSensitiveData = telemetry.EnableSensitiveData;

        // Aspire/標準の OTEL 環境変数(OTEL_EXPORTER_OTLP_ENDPOINT)があれば OTLP を有効化し、その向き先を使う。
        var envEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var envWired = !String.IsNullOrEmpty(envEndpoint);

        if (telemetry.Otlp.Enabled || envWired)
        {
            var tracing = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PcAgent"))
                .AddSource(SourceName)
                .AddSource(DiagnosticsTelemetry.SourceName);

            if (envWired)
            {
                // エンドポイント/プロトコル/ヘッダは OTEL_EXPORTER_OTLP_* から自動取得(Aspire 連携)。
                tracing.AddOtlpExporter();
            }
            else
            {
                tracing.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri(telemetry.Otlp.Endpoint);
                    exporter.Protocol = telemetry.Otlp.Protocol;
                });
            }

            tracerProvider = tracing.Build();

            var metering = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PcAgent"))
                .AddMeter(DiagnosticsMetrics.MeterName);

            if (envWired)
            {
                metering.AddOtlpExporter();
            }
            else
            {
                metering.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri(telemetry.Otlp.Endpoint);
                    exporter.Protocol = telemetry.Otlp.Protocol;
                });
            }

            meterProvider = metering.Build();
        }
    }

    // プロンプト・応答などの機微データをスパンに含めるか(既定オフ)。
    public bool EnableSensitiveData { get; }

    // OTLP 送信が有効か。
    public bool OtlpEnabled => tracerProvider is not null;

    // 保留中のスパン/メトリクスを同期的に送信する(短命な CLI 実行でも確実にエクスポートするため)。
    public void Flush()
    {
        _ = tracerProvider?.ForceFlush(5000);
        _ = meterProvider?.ForceFlush(5000);
    }

    public void Dispose()
    {
        tracerProvider?.Dispose();
        meterProvider?.Dispose();
    }
}
