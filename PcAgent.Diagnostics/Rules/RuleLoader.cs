namespace PcAgent.Diagnostics.Rules;

using System.Text.Json;
using System.Text.Json.Serialization;

// thresholds.json / rules.json を読み込む。呼び出すたびに読み直すため、外部ファイルの変更が即反映される。
internal static class RuleLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static (IReadOnlyDictionary<string, double> Thresholds, IReadOnlyList<RuleDefinition> Rules) Load(string thresholdsPath, string rulesPath) =>
        (LoadThresholds(thresholdsPath), LoadRules(rulesPath));

    private static Dictionary<string, double> LoadThresholds(string path)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            var nested = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(File.ReadAllText(path), Options);
            if (nested is not null)
            {
                foreach (var (group, entries) in nested)
                {
                    foreach (var (key, value) in entries)
                    {
                        result[group + "." + key] = value;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // 不正な JSON は空として扱う。
        }
        catch (IOException)
        {
            // 読み取り不可は空として扱う。
        }

        return result;
    }

    private static List<RuleDefinition> LoadRules(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<RuleDefinition>>(File.ReadAllText(path), Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}
