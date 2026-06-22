namespace PcAgent.Tui.Rendering;

using PcAgent.Diagnostics;
using PcAgent.Diagnostics.Models;
using PcAgent.Diagnostics.Rules;

// 収集→評価をまとめる(コマンドと /diagnose・/health で共有)。
internal static class DiagnosisExecutor
{
    public static async Task<DiagnosisReport> RunAsync(SnapshotBuilder builder, RuleEngine engine, CancellationToken cancellationToken)
    {
        var snapshot = await builder.BuildAsync(cancellationToken);
        var report = engine.Evaluate(snapshot);

        DiagnosticsMetrics.Diagnoses.Add(1);
        foreach (var finding in report.Findings)
        {
            DiagnosticsMetrics.Findings.Add(1, new KeyValuePair<string, object?>("severity", finding.Severity.ToString()));
        }

        return report;
    }
}
