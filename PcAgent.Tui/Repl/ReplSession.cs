namespace PcAgent.Tui.Repl;

using System.Diagnostics;

using Microsoft.Extensions.Options;

using PcAgent.Diagnostics.Collectors;
using PcAgent.Tui.Options;
using PcAgent.Tui.Rendering;

using Spectre.Console;

// 対話 REPL のループ。引数なし起動時に開始する。
public sealed class ReplSession(
    InputDispatcher dispatcher,
    IEnumerable<ISlashCommand> commands,
    IEnumerable<ICollector> collectors,
    CustomCommandLoader customCommandLoader,
    IOptions<UiOptions> uiOptions)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Banner.Show();

        var commandList = Merge(commands, customCommandLoader.Load());
        var slashNames = commandList.Select(static c => c.Name).ToList();
        var sourceNames = collectors.Select(static c => c.Name).OrderBy(static n => n, StringComparer.Ordinal).ToList();

        var reader = CreateReader(slashNames, sourceNames, uiOptions.Value);
        var context = new SlashCommandContext { Commands = commandList };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (!context.ExitRequested && !cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line is null)
                {
                    break;
                }

                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                context.Inputs++;
                await dispatcher.DispatchAsync(trimmed, context, cancellationToken);
            }
        }
        finally
        {
            if (reader is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }

        stopwatch.Stop();
        var bye = "[silver]bye.[/]";
        AnsiConsole.MarkupLine(bye);
        ExitSummary.Show(context.Inputs, context.Findings, stopwatch.Elapsed);
    }

    // 組み込みコマンドにカスタムコマンドを統合する。同名は組み込み優先。
    private static List<ISlashCommand> Merge(IEnumerable<ISlashCommand> builtIn, IReadOnlyList<ISlashCommand> custom)
    {
        var map = new Dictionary<string, ISlashCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in builtIn)
        {
            map[command.Name] = command;
        }

        foreach (var command in custom)
        {
            map.TryAdd(command.Name, command);
        }

        return map.Values.OrderBy(static c => c.Name, StringComparer.Ordinal).ToList();
    }

    private static IInputReader CreateReader(IReadOnlyList<string> slashNames, IReadOnlyList<string> sourceNames, UiOptions ui)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected || ui.CompletionEngine == CompletionEngine.Builtin)
        {
            return new BuiltinInputReader();
        }

        return new PrettyPromptInputReader(slashNames, sourceNames);
    }
}
