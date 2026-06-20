namespace PcAgent.Diagnostics.Collectors;

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

using PcAgent.Diagnostics.Hardware;
using PcAgent.Diagnostics.Models;

// OS・ドライブ・上位プロセスなどのシステム情報(BCL のみ)。管理者権限不要。
public sealed class SystemCollector : ICollector
{
    private const double BytesPerGb = 1024.0 * 1024.0 * 1024.0;

    private const double BytesPerMb = 1024.0 * 1024.0;

    public string Name => "system";

    public string DisplayName => "System";

    public ValueTask<CollectorResult> CollectAsync(CancellationToken cancellationToken)
    {
        var groups = new List<MetricGroup>
        {
            BuildSystemGroup(),
        };

        groups.AddRange(BuildDriveGroups());
        groups.Add(BuildProcessGroup());

        return new ValueTask<CollectorResult>(new CollectorResult(Name, DisplayName, groups, null));
    }

    private static MetricGroup BuildSystemGroup()
    {
        NativeMethods.GetPhysicallyInstalledSystemMemory(out var totalKilobytes);
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture);

        var values = new List<MetricValue>
        {
            new("OS", null, null, RuntimeInformation.OSDescription),
            new("Architecture", null, null, RuntimeInformation.OSArchitecture.ToString()),
            new("Machine", null, null, Environment.MachineName),
            new("User", null, null, Environment.UserName),
            new("Logical Processors", Environment.ProcessorCount, null, null),
            new(".NET Runtime", null, null, RuntimeInformation.FrameworkDescription),
            new("Installed Memory", totalKilobytes / 1024.0 / 1024.0, "GB", null),
            new("Uptime", null, null, uptime),
        };

        return new MetricGroup("System", values);
    }

    private static IEnumerable<MetricGroup> BuildDriveGroups()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            MetricGroup? group = null;
            try
            {
                if (drive.IsReady)
                {
                    var freeGb = drive.AvailableFreeSpace / BytesPerGb;
                    var totalGb = drive.TotalSize / BytesPerGb;
                    var usedPercent = drive.TotalSize > 0
                        ? (1.0 - ((double)drive.AvailableFreeSpace / drive.TotalSize)) * 100.0
                        : 0.0;
                    group = new MetricGroup($"Drive {drive.Name}", [
                        new MetricValue("Type", null, null, drive.DriveType.ToString()),
                        new MetricValue("Free", freeGb, "GB", null),
                        new MetricValue("Total", totalGb, "GB", null),
                        new MetricValue("Used", usedPercent, "%", null),
                    ]);
                }
            }
            catch (IOException)
            {
                group = null;
            }
            catch (UnauthorizedAccessException)
            {
                group = null;
            }

            if (group is not null)
            {
                yield return group;
            }
        }
    }

    private static MetricGroup BuildProcessGroup()
    {
        var processes = Process.GetProcesses();
        try
        {
            var values = processes
                .Select(p => (Name: SafeName(p), WorkingSet: SafeWorkingSet(p)))
                .OrderByDescending(static x => x.WorkingSet)
                .Take(5)
                .Select(static x => new MetricValue(x.Name, x.WorkingSet / BytesPerMb, "MB", null))
                .ToList();

            return new MetricGroup("Top Processes (by working set)", values);
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }

        static string SafeName(Process p)
        {
            try
            {
                return p.ProcessName;
            }
            catch (InvalidOperationException)
            {
                return "(unknown)";
            }
        }

        static long SafeWorkingSet(Process p)
        {
            try
            {
                return p.WorkingSet64;
            }
            catch (InvalidOperationException)
            {
                return 0L;
            }
        }
    }
}
