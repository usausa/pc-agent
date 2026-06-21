namespace PcAgent.Agent.Tools;

using System.ComponentModel;
using System.Globalization;

using PcAgent.Diagnostics.Actions;

// 修復系ツール。ApprovalRequiredAIFunction でラップされ、承認後にのみ実行される。
public static class MaintenanceTools
{
    [Description("一時ファイル(%TEMP%)を削除します。実行には承認が必要です。")]
    public static string CleanTemporaryFiles()
    {
        var result = MaintenanceService.Execute(MaintenanceService.PlanTemp());
        return Format(result);
    }

    [Description("指定ルート配下の Visual Studio プロジェクトの bin/obj を削除します。実行には承認が必要です。")]
    public static string CleanBinObj([Description("探索ルートフォルダの絶対パス")] string root)
    {
        if (String.IsNullOrWhiteSpace(root))
        {
            return "root を指定してください。";
        }

        var result = MaintenanceService.Execute(MaintenanceService.PlanBinObj([root]));
        return Format(result);
    }

    private static string Format(CleanupResult result) =>
        String.Create(CultureInfo.InvariantCulture, $"deleted {result.Deleted}, failed {result.Failed}, freed {result.BytesFreed / (1024.0 * 1024.0):F1} MB");
}
