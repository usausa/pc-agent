namespace PcAgent.Agent;

using Microsoft.Extensions.Options;

using OpenTelemetry;
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

    public AgentTelemetry(IOptions<TelemetryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var telemetry = options.Value;
        EnableSensitiveData = telemetry.EnableSensitiveData;

        if (telemetry.Otlp.Enabled)
        {
            tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PcAgent"))
                .AddSource(SourceName)
                .AddSource(DiagnosticsTelemetry.SourceName)
                .AddOtlpExporter(exporter => exporter.Endpoint = new Uri(telemetry.Otlp.Endpoint))
                .Build();
        }
    }

    // プロンプト・応答などの機微データをスパンに含めるか(既定オフ)。
    public bool EnableSensitiveData { get; }

    // OTLP 送信が有効か。
    public bool OtlpEnabled => tracerProvider is not null;

    public void Dispose() => tracerProvider?.Dispose();
}
