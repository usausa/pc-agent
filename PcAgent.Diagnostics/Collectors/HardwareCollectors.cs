namespace PcAgent.Diagnostics.Collectors;

using PcAgent.Diagnostics.Hardware;

// CPU。
public sealed class CpuCollector(HardwareMonitorSource source) : HardwareCollectorBase(source, SensorScope.Cpu)
{
    public override string Name => "cpu";

    public override string DisplayName => "CPU";
}

// GPU(NVIDIA / AMD / Intel)。
public sealed class GpuCollector(HardwareMonitorSource source) : HardwareCollectorBase(source, SensorScope.Gpu)
{
    public override string Name => "gpu";

    public override string DisplayName => "GPU";
}

// メモリ。
public sealed class MemoryCollector(HardwareMonitorSource source) : HardwareCollectorBase(source, SensorScope.Memory)
{
    public override string Name => "memory";

    public override string DisplayName => "Memory";
}

// マザーボード / Super I/O。
public sealed class MotherboardCollector(HardwareMonitorSource source) : HardwareCollectorBase(source, SensorScope.Motherboard)
{
    public override string Name => "motherboard";

    public override string DisplayName => "Motherboard";
}

// ストレージ(ドライブ温度・寿命など)。
public sealed class DiskCollector(HardwareMonitorSource source) : HardwareCollectorBase(source, SensorScope.Disk)
{
    public override string Name => "disk";

    public override string DisplayName => "Storage";
}

// ネットワーク。
public sealed class NetworkCollector(HardwareMonitorSource source) : HardwareCollectorBase(source, SensorScope.Network)
{
    public override string Name => "network";

    public override string DisplayName => "Network";
}

// バッテリー。
public sealed class BatteryCollector(HardwareMonitorSource source) : HardwareCollectorBase(source, SensorScope.Battery)
{
    public override string Name => "battery";

    public override string DisplayName => "Battery";
}
