namespace PcAgent.Diagnostics.Platform;

using System.Security.Principal;

// 現在のプロセスが管理者権限で実行されているかを判定する。
public static class AdminChecker
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
