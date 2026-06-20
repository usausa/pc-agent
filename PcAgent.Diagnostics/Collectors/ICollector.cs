namespace PcAgent.Diagnostics.Collectors;

using PcAgent.Diagnostics.Models;

// 情報収集の拡張ポイント。実装を DI に登録するだけで info の対象カテゴリが増える。
public interface ICollector
{
    // カテゴリ名(info コマンドの --category に対応)。
    string Name { get; }

    // 表示名。
    string DisplayName { get; }

    // 情報を収集する。
    ValueTask<CollectorResult> CollectAsync(CancellationToken cancellationToken);
}
