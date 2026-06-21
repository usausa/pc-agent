namespace PcAgent.Tui.Options;

// カスタムコマンド等のユーザー拡張に関する設定。
public sealed class CustomizationOptions
{
    // 設定セクション名。
    public const string SectionName = "Customization";

    // カスタム /コマンド の探索パス(グローバル → プロジェクトの順、同名はプロジェクト優先)。
    // 未設定時は既定パス(~/.pcagent/commands, .pcagent/commands)を使う。
    public IList<string> CommandsPaths { get; } = [];
}
