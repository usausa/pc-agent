namespace PcAgent.Diagnostics.Options;

// 修復アクションに関する設定。
public sealed class ActionsOptions
{
    // 設定セクション名。
    public const string SectionName = "Actions";

    // 修復アクションを有効にするか。
    public bool Enabled { get; set; } = true;

    // 修復前に承認を必須とするか。
    public bool RequireApproval { get; set; } = true;

    // シェル実行(! コマンド)を許可するか。
    public bool AllowShell { get; set; } = true;
}
