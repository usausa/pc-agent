namespace PcAgent.Diagnostics.Hardware;

// 1つのセンサーの読み取り値(Value が null なら取得不可)。
public readonly record struct SensorReading(string Name, SensorKind Kind, double? Value, int Index);

// 1つのハードウェアコンポーネントの読み取り値。
public sealed record ComponentReading(string Type, string Name, IReadOnlyList<SensorReading> Sensors);
