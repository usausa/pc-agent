namespace PcAgent.Tui.Options;

// 装飾レベル。
public enum DecorationLevel
{
    // 絵文字・進捗グラフを多用する。
    Rich,

    // ASCII 中心(安全側)。
    Safe,
}

// 入力補完エンジン。
public enum CompletionEngine
{
    // PrettyPrompt を使用する。
    PrettyPrompt,

    // 自前の簡易補完を使用する。
    Builtin,
}

// UI(TUI)に関する設定。
public sealed class UiOptions
{
    // 設定セクション名。
    public const string SectionName = "Ui";

    // 装飾レベル。
    public DecorationLevel DecorationLevel { get; set; } = DecorationLevel.Rich;

    // 本文をスクロールバックへ逐次出力するか。
    public bool StreamBodyToScrollback { get; set; } = true;

    // 入力補完エンジン。
    public CompletionEngine CompletionEngine { get; set; } = CompletionEngine.PrettyPrompt;
}
