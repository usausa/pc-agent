namespace PcAgent.Diagnostics.Hardware;

// センサーの種別(ハードウェアライブラリ非依存の中立表現)。
public enum SensorKind
{
    Temperature,
    Load,
    Clock,
    Voltage,
    Current,
    Power,
    Fan,
    Control,
    Level,
    Factor,
    Data,
    SmallData,
    Throughput,
    Energy,
    Duration,
    Other,
}

// 収集対象のハードウェアスコープ。
public enum SensorScope
{
    Cpu,
    Gpu,
    Memory,
    Motherboard,
    Disk,
    Network,
    Battery,
}
