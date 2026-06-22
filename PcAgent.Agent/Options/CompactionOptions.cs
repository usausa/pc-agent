namespace PcAgent.Agent.Options;

// 会話履歴の圧縮(Compaction)に関する設定。
public sealed class CompactionOptions
{
    // 設定セクション名。
    public const string SectionName = "Compaction";

    // 圧縮を有効にするか。
    public bool Enabled { get; set; } = true;

    // メッセージ数がこの値を超えたら圧縮する(CompactionTriggers.MessagesExceed)。
    public int MessagesThreshold { get; set; } = 30;

    // 圧縮時に残す直近ターン数(SlidingWindowCompactionStrategy)。
    public int KeepRecentTurns { get; set; } = 2;
}
