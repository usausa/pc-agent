namespace PcAgent.Diagnostics.Collectors;

using System.Diagnostics.Eventing.Reader;
using System.Globalization;

using PcAgent.Diagnostics.Models;

// 直近 24 時間のイベントログ(System / Application)のエラー・警告サマリ(System.Diagnostics.EventLog)。
// 同一の (プロバイダ + イベント ID) は集約して件数表示する。
public sealed class EventLogCollector : ICollector
{
    private const int MaxScan = 1000;           // 走査上限(件数カウント)。
    private const int TopDistinct = 6;          // 表示する種類数(件数の多い順)。
    private const int MessageMaxLength = 80;    // メッセージ表示の最大長。

    // 直近 24 時間(86,400,000 ms)の Critical/Error/Warning を新しい順に取得する XPath。
    private const string RecentQuery =
        "*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= 86400000]]]";

    private static readonly string[] Logs = ["System", "Application"];

    public string Name => "eventlog";

    public string DisplayName => "Event Log";

    public ValueTask<CollectorResult> CollectAsync(CancellationToken cancellationToken) => new(Collect());

    private CollectorResult Collect()
    {
        var groups = new List<MetricGroup>(Logs.Length);
        var failures = new List<string>();

        foreach (var log in Logs)
        {
            try
            {
                groups.Add(ReadLog(log));
            }
            catch (EventLogException ex)
            {
                failures.Add($"{log}: {ex.Message}");
            }
            catch (UnauthorizedAccessException)
            {
                failures.Add($"{log}: アクセスが拒否されました(管理者権限が必要な場合があります)。");
            }
        }

        var note = failures.Count > 0 ? String.Join(" / ", failures) : null;
        return new CollectorResult(Name, DisplayName, groups, note);
    }

    private static MetricGroup ReadLog(string log)
    {
        var query = new EventLogQuery(log, PathType.LogName, RecentQuery)
        {
            ReverseDirection = true,
            TolerateQueryErrors = true,
        };

        var critical = 0;
        var errors = 0;
        var warnings = 0;
        var distinct = new Dictionary<string, EventAggregate>(StringComparer.Ordinal);

        var scanned = 0;
        using (var reader = new EventLogReader(query))
        {
            for (; scanned < MaxScan; scanned++)
            {
                using var record = reader.ReadEvent();
                if (record is null)
                {
                    break;
                }

                switch (record.Level)
                {
                    case 1: critical++; break;
                    case 2: errors++; break;
                    case 3: warnings++; break;
                    default: break;
                }

                var key = $"{record.Level}|{record.ProviderName}|{record.Id}";
                if (distinct.TryGetValue(key, out var existing))
                {
                    existing.Count++;
                }
                else
                {
                    distinct[key] = NewAggregate(record);
                }
            }
        }

        var values = new List<MetricValue>
        {
            new("Critical", critical, null, null),
            new("Errors", errors, null, null),
            new("Warnings", warnings, null, null),
        };

        if (scanned >= MaxScan)
        {
            values.Add(new("Scan", null, null, $"上限 {MaxScan.ToString(CultureInfo.InvariantCulture)} 件で打ち切り(実際はそれ以上)"));
        }

        foreach (var aggregate in distinct.Values.OrderByDescending(static a => a.Count).ThenByDescending(static a => a.Sort).Take(TopDistinct))
        {
            var times = aggregate.Count > 1 ? $" ×{aggregate.Count.ToString(CultureInfo.InvariantCulture)}" : String.Empty;
            var id = aggregate.Id.ToString(CultureInfo.InvariantCulture);
            values.Add(new($"{aggregate.Time} {aggregate.Provider}", null, null, $"[{LevelName(aggregate.Level)} #{id}]{times} {aggregate.Message}"));
        }

        return new MetricGroup(log + " (24h)", values);
    }

    private static EventAggregate NewAggregate(EventRecord record)
    {
        var created = record.TimeCreated ?? DateTime.MinValue;
        var time = record.TimeCreated is { } t ? t.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) : "--";
        var provider = String.IsNullOrEmpty(record.ProviderName) ? "(unknown)" : record.ProviderName;
        return new EventAggregate
        {
            Level = record.Level,
            Provider = provider,
            Id = record.Id,
            Time = time,
            Sort = created,
            Message = SafeMessage(record),
            Count = 1,
        };
    }

    private static string SafeMessage(EventRecord record)
    {
        string? message;
        try
        {
            message = record.FormatDescription();
        }
        catch (EventLogException)
        {
            message = null;
        }

        if (String.IsNullOrWhiteSpace(message))
        {
            return "(メッセージなし)";
        }

        var line = message.ReplaceLineEndings(" ").Trim();
        return line.Length > MessageMaxLength ? line[..MessageMaxLength] + "…" : line;
    }

    private static string LevelName(byte? level) => level switch
    {
        1 => "Critical",
        2 => "Error",
        3 => "Warning",
        4 => "Information",
        _ => "?",
    };

    // (プロバイダ + イベント ID) 単位の集約。
    private sealed class EventAggregate
    {
        public required byte? Level { get; init; }

        public required string Provider { get; init; }

        public required int Id { get; init; }

        public required string Time { get; init; }

        public required DateTime Sort { get; init; }

        public required string Message { get; init; }

        public int Count { get; set; }
    }
}
