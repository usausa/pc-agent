namespace PcAgent.Diagnostics.Rules;

using PcAgent.Diagnostics.Models;

// 外部 JSON(rules.json)の 1 ルール。Value=リテラル閾値 / ValueRef=閾値ファイルのキー参照。
public sealed class RuleDefinition
{
    public string Id { get; set; } = String.Empty;

    public string Metric { get; set; } = String.Empty;

    public string Op { get; set; } = ">";

    public double? Value { get; set; }

    public string? ValueRef { get; set; }

    public Severity Severity { get; set; }

    public string Message { get; set; } = String.Empty;

    public string? Action { get; set; }
}
