namespace PcAgent.Tui.Commands;

using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

// ルートコマンド。引数なしで対話 REPL(フェーズ5)、--ask で単発質問(フェーズ3)を担う。
public sealed class RootCommandHandler(ILogger<RootCommandHandler> log) : ICommandHandler
{
    // 単発で投げる質問。
    [Option<string>("--ask", "-a", Description = "Ask a single question and exit")]
    public string? Ask { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!string.IsNullOrWhiteSpace(Ask))
        {
            log.LogInformation("Single-shot question received (agent wiring arrives in phase 3): {Question}", Ask);
        }
        else
        {
            log.LogInformation("Interactive REPL is not available yet (arrives in phase 5). Run with --help to list commands.");
        }

        return ValueTask.CompletedTask;
    }
}
