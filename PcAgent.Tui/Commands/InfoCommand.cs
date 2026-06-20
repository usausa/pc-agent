namespace PcAgent.Tui.Commands;

using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

// 指定カテゴリの生情報を表示する(収集はフェーズ2で実装)。
[Command("info", "Show raw PC information for a category")]
public sealed class InfoCommand(ILogger<InfoCommand> log) : ICommandHandler
{
    // 情報カテゴリ。
    [Option<string>("--category", "-c", Description = "Information category: cpu, gpu, memory, disk, smart, network, system")]
    public string? Category { get; set; }

    // JSON で出力するか。
    [Option("--json", "-j", Description = "Output as JSON")]
    public bool Json { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("info command (collectors arrive in phase 2): category={Category}, json={Json}", Category ?? "(all)", Json);
        return ValueTask.CompletedTask;
    }
}
