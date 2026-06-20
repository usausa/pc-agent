namespace PcAgent.Agent.Tools;

using System.ComponentModel;

using PcAgent.Diagnostics.Collectors;

// コレクターをラップした関数ツール群。[Description] を付けるだけでエージェントのツールになる。
public sealed class PcInfoTools(IEnumerable<ICollector> collectors)
{
    [Description("指定カテゴリのPC情報(センサー値/SMART/システム情報)を取得します。")]
    public async Task<string> GetPcInfo(
        [Description("情報カテゴリ。cpu, gpu, memory, motherboard, disk, network, battery, smart, system のいずれか。")] string category)
    {
        var collector = collectors.FirstOrDefault(c => String.Equals(c.Name, category, StringComparison.OrdinalIgnoreCase));
        if (collector is null)
        {
            return "Unknown category: " + category + ". Available: " + String.Join(", ", collectors.Select(static c => c.Name));
        }

        var result = await collector.CollectAsync(CancellationToken.None).ConfigureAwait(false);
        return ToolFormatter.Format(result);
    }

    [Description("利用可能なPC情報カテゴリの一覧を返します。")]
    public string ListCategories() => String.Join(", ", collectors.Select(static c => c.Name));
}
