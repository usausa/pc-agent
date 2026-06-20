namespace PcAgent.Diagnostics.Options;

// 情報収集に関する設定。
public sealed class CollectionOptions
{
    // 設定セクション名。
    public const string SectionName = "Collection";

    // センサー収集の節流間隔(ミリ秒)。
    public int UpdateIntervalMs { get; set; } = 1000;

    // SMART 収集の間隔(ミリ秒)。
    public int SmartIntervalMs { get; set; } = 10000;
}
