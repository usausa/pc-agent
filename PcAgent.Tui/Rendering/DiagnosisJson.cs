namespace PcAgent.Tui.Rendering;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using PcAgent.Diagnostics.Models;

// 診断レポートの JSON 化(--json / /report save 用)。
internal static class DiagnosisJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(DiagnosisReport report) => JsonSerializer.Serialize(report, Options);
}
