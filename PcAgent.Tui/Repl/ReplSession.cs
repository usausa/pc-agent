namespace PcAgent.Tui.Repl;

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
    IOptions<UiOptions> uiOptions)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Banner.Show();

        var commandList = commands.OrderBy(static c => c.Name, StringComparer.Ordinal).ToList();
        var slashNames = commandList.Select(static c => c.Name).ToList();
        var sourceNames = collectors.Select(static c => c.Name).OrderBy(static n => n, StringComparer.Ordinal).ToList();

        var reader = CreateReader(slashNames, sourceNames, uiOptions.Value);
        var context = new SlashCommandContext { Commands = commandList };

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

        var bye = "[silver]bye.[/]";
        AnsiConsole.MarkupLine(bye);
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
