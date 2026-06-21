namespace PcAgent.Diagnostics.Rules;

using Microsoft.Extensions.Options;

using PcAgent.Diagnostics.Models;
using PcAgent.Diagnostics.Options;

// 外部ルール/閾値でスナップショットを評価し、診断レポートを決定的に生成する(LLM 非依存)。
public sealed class RuleEngine(IOptions<DiagnosticsOptions> options)
{
    public DiagnosisReport Evaluate(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var (thresholds, rules) = RuleLoader.Load(Resolve(options.Value.ThresholdsPath), Resolve(options.Value.RulesPath));

        var findings = new List<Finding>();
        foreach (var rule in rules)
        {
            if (ResolveThreshold(rule, thresholds) is not { } limit)
            {
                continue;
            }

            foreach (var metric in snapshot.Metrics)
            {
                if (String.Equals(metric.Path, rule.Metric, StringComparison.OrdinalIgnoreCase) && Compare(metric.Value, rule.Op, limit))
                {
                    findings.Add(new Finding(rule.Id, rule.Metric, rule.Severity, rule.Message, metric.Source, metric.Value, limit, rule.Action));
                }
            }
        }

        // 同じメトリクス×出所では最も深刻な指摘だけを残す。
        var deduped = findings
            .GroupBy(static f => (f.Metric, f.Source))
            .Select(static g => g.MaxBy(static x => x.Severity)!)
            .OrderByDescending(static f => f.Severity)
            .ToList();

        var overall = deduped.Count == 0 ? Severity.Ok : deduped[0].Severity;
        var actions = deduped
            .Where(static f => !String.IsNullOrEmpty(f.Action))
            .Select(static f => f.Action!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new DiagnosisReport(snapshot.Timestamp, overall, deduped, actions);
    }

    // 読み込めるルール/閾値の件数を返す(/rules 表示用)。
    public (int Thresholds, int Rules) Describe()
    {
        var (thresholds, rules) = RuleLoader.Load(Resolve(options.Value.ThresholdsPath), Resolve(options.Value.RulesPath));
        return (thresholds.Count, rules.Count);
    }

    private static double? ResolveThreshold(RuleDefinition rule, IReadOnlyDictionary<string, double> thresholds)
    {
        if (rule.Value is { } value)
        {
            return value;
        }

        return !String.IsNullOrEmpty(rule.ValueRef) && thresholds.TryGetValue(rule.ValueRef, out var threshold) ? threshold : null;
    }

    private static bool Compare(double actual, string op, double threshold) => op switch
    {
        "<" => actual < threshold,
        "<=" => actual <= threshold,
        ">" => actual > threshold,
        ">=" => actual >= threshold,
        "==" => Math.Abs(actual - threshold) < 1e-9,
        "!=" => Math.Abs(actual - threshold) >= 1e-9,
        _ => false,
    };

    private static string Resolve(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
