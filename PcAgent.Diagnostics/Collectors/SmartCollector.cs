namespace PcAgent.Diagnostics.Collectors;

using System.Globalization;

using HardwareInfo.Disk;

using PcAgent.Diagnostics.Models;
using PcAgent.Diagnostics.Platform;

// ディスクの SMART 情報(NVMe / Generic)。管理者権限が必要。
public sealed class SmartCollector : ICollector
{
    public string Name => "smart";

    public string DisplayName => "Disk SMART";

    public ValueTask<CollectorResult> CollectAsync(CancellationToken cancellationToken)
    {
        var groups = new List<MetricGroup>();
        var disks = DiskInfo.GetInformation();
        try
        {
            foreach (var disk in disks)
            {
                groups.Add(ReadDisk(disk));
            }
        }
        finally
        {
            foreach (var disk in disks)
            {
                disk.Dispose();
            }
        }

        var note = AdminChecker.IsAdministrator() ? null : "管理者権限がない場合、SMART 情報を取得できないことがあります。";
        return new ValueTask<CollectorResult>(new CollectorResult(Name, DisplayName, groups, note));
    }

    private static MetricGroup ReadDisk(IDiskInfo disk)
    {
        var drive = String.Concat(disk.GetDrives().Select(static d => d.Name.TrimEnd(':')));
        var name = String.IsNullOrEmpty(drive) ? disk.Model : $"{disk.Model} ({drive})";
        var values = new List<MetricValue>();

        if (disk.SmartType == SmartType.Nvme && disk.Smart is ISmartNvme nvme)
        {
            if (nvme.Update())
            {
                values.Add(new("Percentage Used", nvme.PercentageUsed, "%", null));
                values.Add(new("Power-On Hours", nvme.PowerOnHours, "h", null));
                values.Add(new("Temperature", nvme.Temperature, "°C", null));
                values.Add(new("Available Spare", nvme.AvailableSpare, "%", null));
                values.Add(new("Available Spare Threshold", nvme.AvailableSpareThreshold, "%", null));
                values.Add(new("Critical Warning", nvme.CriticalWarning, null, null));
                values.Add(new("Media Errors", nvme.MediaErrors, null, null));
                values.Add(new("Unsafe Shutdowns", nvme.UnsafeShutdowns, null, null));
                values.Add(new("Power Cycles", nvme.PowerCycles, null, null));
                values.Add(new("Data Unit Read", nvme.DataUnitRead, null, null));
                values.Add(new("Data Unit Written", nvme.DataUnitWritten, null, null));
            }
        }
        else if (disk.SmartType == SmartType.Generic && disk.Smart is ISmartGeneric generic)
        {
            if (generic.Update())
            {
                foreach (var id in generic.GetSupportedIds())
                {
                    var attribute = generic.GetAttribute(id);
                    values.Add(new(SmartNames.Resolve((byte)id), attribute?.RawValue, null, null));
                }
            }
        }

        return new MetricGroup(name, values);
    }
}

// 代表的な ATA SMART 属性 ID の名称。
internal static class SmartNames
{
    public static string Resolve(byte id) => id switch
    {
        0x05 => "Reallocated Sectors Count",
        0x09 => "Power-On Hours",
        0x0C => "Power Cycle Count",
        0xB7 => "SSD Wear Indicator",
        0xC2 => "Temperature",
        0xC5 => "Current Pending Sector Count",
        0xC6 => "Uncorrectable Sector Count",
        0xC7 => "UltraDMA CRC Error Count",
        0xE7 => "SSD Life Left",
        0xE9 => "Media Wearout Indicator",
        0xF1 => "Total LBAs Written",
        0xF2 => "Total LBAs Read",
        _ => "SMART " + id.ToString("X2", CultureInfo.InvariantCulture),
    };
}
