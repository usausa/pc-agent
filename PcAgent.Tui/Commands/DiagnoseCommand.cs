namespace PcAgent.Tui.Commands;

using PcAgent.Diagnostics;
using PcAgent.Diagnostics.Rules;
using PcAgent.Tui.Rendering;

using Smart.CommandLine.Hosting;

// 診断を実行して結果を表示する(非対話の単発コマンド)。
[Command("diagnose", "Run diagnostics and report findings")]
public sealed class DiagnoseCommand(SnapshotBuilder builder, RuleEngine engine) : ICommandHandler
{
    // JSON で出力するか。
    [Option("--json", "-j", Description = "Output as JSON")]
    public bool Json { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var report = await DiagnosisExecutor.RunAsync(builder, engine, context.CancellationToken);

        if (Json)
        {
            var json = DiagnosisJson.Serialize(report);
            await Console.Out.WriteLineAsync(json);
        }
        else
        {
            DiagnosisRenderer.Render(report);
        }
    }
}
