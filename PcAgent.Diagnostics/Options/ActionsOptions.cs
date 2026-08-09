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

    // シェル実行(! コマンド / LLM シェルツール)を許可するか。
    public bool AllowShell { get; set; } = true;

    // LLM 起動シェルツールの制限設定。
    public ShellActionOptions Shell { get; } = new();
}

// LLM 起動シェルツールの制限。承認に加え、実行可能コマンドを許可リストで絞る。
public sealed class ShellActionOptions
{
    // LLM が実行できるコマンド名(先頭語)の許可リスト。空なら全拒否。
    public IList<string> AllowedCommands { get; } = [];

    // 1 回の実行に許す最大秒数。ping -t のような終了しないコマンドを打ち切る。
    public int TimeoutSeconds { get; set; } = 30;
}
