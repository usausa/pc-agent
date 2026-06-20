namespace PcAgent.Diagnostics.Collectors;

using PcAgent.Diagnostics.Hardware;
using PcAgent.Diagnostics.Models;

// LibreHardwareMonitor 由来のハードウェアカテゴリ収集の基底。スコープを指定するだけで派生を作れる。
public abstract class HardwareCollectorBase(HardwareMonitorSource source, SensorScope scope) : ICollector
{
    public abstract string Name { get; }

    public abstract string DisplayName { get; }

    public ValueTask<CollectorResult> CollectAsync(CancellationToken cancellationToken)
    {
        var components = source.Read(scope);
        var groups = components
            .Select(static component => new MetricGroup(component.Name, [.. component.Sensors.Select(MapSensor)]))
            .ToList();
        return new ValueTask<CollectorResult>(new CollectorResult(Name, DisplayName, groups, null));
    }

    private static MetricValue MapSensor(SensorReading reading)
    {
        var (unit, divisor) = UnitOf(reading.Kind);
        var value = reading.Value is { } raw ? raw / divisor : (double?)null;
        return new MetricValue(reading.Name, value, String.IsNullOrEmpty(unit) ? null : unit, null);
    }

    private static (string Unit, double Divisor) UnitOf(SensorKind kind) => kind switch
    {
        SensorKind.Temperature => ("°C", 1.0),
        SensorKind.Load => ("%", 1.0),
        SensorKind.Clock => ("MHz", 1.0),
        SensorKind.Voltage => ("V", 1.0),
        SensorKind.Current => ("A", 1.0),
        SensorKind.Power => ("W", 1.0),
        SensorKind.Fan => ("RPM", 1.0),
        SensorKind.Control => ("%", 1.0),
        SensorKind.Level => ("%", 1.0),
        SensorKind.Data => ("GB", 1.0),
        SensorKind.SmallData => ("MB", 1.0),
        SensorKind.Throughput => ("MB/s", 1024.0 * 1024.0),
        SensorKind.Energy => ("mWh", 1.0),
        SensorKind.Duration => ("s", 1.0),
        _ => (String.Empty, 1.0),
    };
}
