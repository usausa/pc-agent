namespace PcAgent.Tui.Rendering;

using System.Text.Json.Serialization;

using PcAgent.Diagnostics.Models;

// info --json 出力用の System.Text.Json ソース生成コンテキスト。
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<CollectorResult>))]
internal sealed partial class CollectorJsonContext : JsonSerializerContext;
