namespace PcAgent.Tui.Commands;

using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

// 診断を実行して結果を表示する(ルールエンジンはフェーズ6で実装)。
[Command("diagnose", "Run diagnostics and report findings")]
public sealed class DiagnoseCommand(ILogger<DiagnoseCommand> log) : ICommandHandler
{
    // 診断対象を限定するカテゴリ。
    [Option<string>("--category", "-c", Description = "Limit diagnosis to a category")]
    public string? Category { get; set; }

    // JSON で出力するか。
    [Option("--json", "-j", Description = "Output as JSON")]
    public bool Json { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("diagnose command (rule engine arrives in phase 6): category={Category}, json={Json}", Category ?? "(all)", Json);
        return ValueTask.CompletedTask;
    }
}
