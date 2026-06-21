namespace PcAgent.Tui.Repl;

using System.IO;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PcAgent.Agent;
using PcAgent.Diagnostics.Collectors;
using PcAgent.Tui.Options;

// Customization:CommandsPaths から Markdown カスタムコマンドを読み込む。
// 2 層(グローバル → プロジェクト)で読み、同名はプロジェクト優先(後勝ち)。
public sealed partial class CustomCommandLoader(
    IOptions<CustomizationOptions> options,
    IEnumerable<ICollector> collectors,
    ShellRunner shell,
    IAgentConversation conversation,
    ILogger<CustomCommandLoader> logger)
{
    private static readonly string[] DefaultPaths = ["~/.pcagent/commands", ".pcagent/commands"];

    public IReadOnlyList<ISlashCommand> Load()
    {
        var collectorList = collectors.ToList();
        var map = new Dictionary<string, ISlashCommand>(StringComparer.OrdinalIgnoreCase);

        var configured = options.Value.CommandsPaths;
        var paths = configured.Count > 0 ? configured : DefaultPaths;

        foreach (var raw in paths)
        {
            var directory = ResolvePath(raw);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
            {
                var command = TryParse(file, collectorList);
                if (command is not null)
                {
                    map[command.Name] = command;
                }
            }
        }

        return map.Values.ToList();
    }

    private CustomCommand? TryParse(string path, IReadOnlyList<ICollector> collectorList)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            LogLoadFailed(logger, path, ex.Message);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogLoadFailed(logger, path, ex.Message);
            return null;
        }

        var parsed = FrontMatter.Parse(text);
        if (parsed is null)
        {
            return null;
        }

        var name = parsed.Value.Name;
        if (String.IsNullOrWhiteSpace(name))
        {
            name = Path.GetFileNameWithoutExtension(path);
        }

        return new CustomCommand(name, parsed.Value.Description, parsed.Value.ArgumentHint, parsed.Value.Body, collectorList, shell, conversation);
    }

    private static string ResolvePath(string raw)
    {
        var path = raw;
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(home, path[2..]);
        }

        return Path.GetFullPath(path);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Custom command load failed: {Path} ({Reason})")]
    private static partial void LogLoadFailed(ILogger logger, string path, string reason);
}
