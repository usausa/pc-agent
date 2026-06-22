namespace PcAgent.Tui.Repl;

// 直近のエージェント応答テキストを保持する(/copy・/save 用)。
public sealed class LastResponseStore
{
    // 直近の応答(未取得なら null)。
    public string? Text { get; set; }
}
