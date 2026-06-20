namespace PcAgent.Diagnostics.Options;

// 診断(ルール・閾値)に関する設定。
public sealed class DiagnosticsOptions
{
    // 設定セクション名。
    public const string SectionName = "Diagnostics";

    // 閾値ファイルのパス。
    public string ThresholdsPath { get; set; } = "rules/thresholds.json";

    // ルールファイルのパス。
    public string RulesPath { get; set; } = "rules/rules.json";
}
