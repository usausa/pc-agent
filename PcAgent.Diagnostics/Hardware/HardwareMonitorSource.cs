namespace PcAgent.Diagnostics.Hardware;

using System.Threading;

using LibreHardwareMonitor.Hardware;

using Microsoft.Extensions.Options;

using PcAgent.Diagnostics.Options;

// LibreHardwareMonitor の Computer をラップし、スレッドセーフに開閉・更新・読み取りを行う。
public sealed class HardwareMonitorSource : IDisposable
{
    private readonly Lock sync = new();

    private readonly Computer computer;

    private readonly UpdateVisitor visitor = new();

    private readonly TimeSpan throttle;

    private bool opened;

    private bool updated;

    private DateTime lastUpdate;

    public HardwareMonitorSource(IOptions<CollectionOptions> options)
    {
        throttle = TimeSpan.FromMilliseconds(options.Value.UpdateIntervalMs);
        computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = true,
            IsStorageEnabled = true,
            IsBatteryEnabled = true,
        };
    }

    // 指定スコープのコンポーネント読み取り値を返す。Computer へのアクセスはロック内で完結させる。
    public IReadOnlyList<ComponentReading> Read(SensorScope scope)
    {
        var types = MapScope(scope);
        lock (sync)
        {
            EnsureUpdated();

            var components = new List<ComponentReading>();
            foreach (var hardware in EnumerateHardware(computer.Hardware))
            {
                if (Array.IndexOf(types, hardware.HardwareType) < 0)
                {
                    continue;
                }

                var readings = new List<SensorReading>();
                foreach (var sensor in hardware.Sensors)
                {
                    readings.Add(new SensorReading(sensor.Name, MapKind(sensor.SensorType), ToValue(sensor), sensor.Index));
                }

                components.Add(new ComponentReading(hardware.HardwareType.ToString(), hardware.Name.TrimEnd('\0'), readings));
            }

            return components;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (opened)
            {
                computer.Close();
                opened = false;
            }
        }
    }

    private void EnsureUpdated()
    {
        if (!opened)
        {
            computer.Open();
            opened = true;
            computer.Accept(visitor);
            lastUpdate = DateTime.Now;
            updated = true;
            return;
        }

        var now = DateTime.Now;
        if (updated && ((now - lastUpdate) < throttle))
        {
            return;
        }

        computer.Accept(visitor);
        lastUpdate = now;
        updated = true;
    }

    private static IEnumerable<IHardware> EnumerateHardware(IEnumerable<IHardware> source)
    {
        foreach (var hardware in source)
        {
            yield return hardware;
            foreach (var sub in EnumerateHardware(hardware.SubHardware))
            {
                yield return sub;
            }
        }
    }

    private static double? ToValue(ISensor sensor)
    {
        if (sensor.Value is not { } value || Single.IsNaN(value))
        {
            return null;
        }

        return value;
    }

    private static HardwareType[] MapScope(SensorScope scope) => scope switch
    {
        SensorScope.Cpu => [HardwareType.Cpu],
        SensorScope.Gpu => [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel],
        SensorScope.Memory => [HardwareType.Memory],
        SensorScope.Motherboard => [HardwareType.Motherboard, HardwareType.SuperIO],
        SensorScope.Disk => [HardwareType.Storage],
        SensorScope.Network => [HardwareType.Network],
        SensorScope.Battery => [HardwareType.Battery],
        _ => [],
    };

    private static SensorKind MapKind(SensorType type) => type switch
    {
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Load => SensorKind.Load,
        SensorType.Clock => SensorKind.Clock,
        SensorType.Voltage => SensorKind.Voltage,
        SensorType.Current => SensorKind.Current,
        SensorType.Power => SensorKind.Power,
        SensorType.Fan => SensorKind.Fan,
        SensorType.Control => SensorKind.Control,
        SensorType.Level => SensorKind.Level,
        SensorType.Factor => SensorKind.Factor,
        SensorType.Data => SensorKind.Data,
        SensorType.SmallData => SensorKind.SmallData,
        SensorType.Throughput => SensorKind.Throughput,
        SensorType.Energy => SensorKind.Energy,
        SensorType.TimeSpan => SensorKind.Duration,
        _ => SensorKind.Other,
    };
}
