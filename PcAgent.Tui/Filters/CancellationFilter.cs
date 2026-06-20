namespace PcAgent.Tui.Filters;

using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

// 取り消し(Ctrl+C / Esc)を捕捉して穏当に終了するグローバルフィルタ。
// 一般的な例外ハンドリングはフェーズ9で拡張する。
public sealed class CancellationFilter(ILogger<CancellationFilter> log) : ICommandFilter
{
    public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning("Operation was canceled.");
            context.ExitCode = 130;
        }
    }
}
