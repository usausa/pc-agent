namespace PcAgent.Agent.Rag;

using System.Globalization;
using System.IO;

using Microsoft.Agents.AI;

using PcAgent.Diagnostics.Platform;

// 実行のたびに現在状況(時刻・マシン・権限・最も使用率の高いドライブ)を Instructions に注入する。
// 注入内容はシステム由来の信頼できる値のみ(プロンプトインジェクション対策)。
internal sealed class PcContextProvider : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        var admin = AdminChecker.IsAdministrator() ? "あり" : "なし";
        var note = String.Create(
            CultureInfo.InvariantCulture,
            $"[現在の状況] 時刻: {now} / マシン: {Environment.MachineName} / 管理者権限: {admin}{DriveSummary()}");

        var aiContext = new AIContext { Instructions = note };
        return ValueTask.FromResult(aiContext);
    }

    private static string DriveSummary()
    {
        string? worst = null;
        var maxUsed = -1.0;

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if ((drive.DriveType != DriveType.Fixed) || !drive.IsReady || (drive.TotalSize <= 0))
                {
                    continue;
                }

                var used = (1.0 - ((double)drive.AvailableFreeSpace / drive.TotalSize)) * 100.0;
                if (used > maxUsed)
                {
                    maxUsed = used;
                    worst = drive.Name;
                }
            }
            catch (IOException)
            {
                // 読み取り不可はスキップ。
            }
            catch (UnauthorizedAccessException)
            {
                // アクセス不可はスキップ。
            }
        }

        return worst is null
            ? String.Empty
            : String.Create(CultureInfo.InvariantCulture, $" / 最も使用率の高いドライブ: {worst} {maxUsed:F0}%");
    }
}
