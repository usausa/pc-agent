namespace PcAgent.Tui.Filters;

using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

// コマンド全体の処理時間を計測してログ出力するグローバルフィルタ。
public sealed class ExecutionTimeFilter(ILogger<ExecutionTimeFilter> log) : ICommandFilter
{
    public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();
        log.LogInformation("Command {Command} finished in {Elapsed} ms", context.CommandType.Name, stopwatch.ElapsedMilliseconds);
    }
}
