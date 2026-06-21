namespace PcAgent.Tui.Repl;

using System.Globalization;

using Microsoft.Extensions.Options;

using PcAgent.Diagnostics;
using PcAgent.Diagnostics.Options;
using PcAgent.Diagnostics.Rules;
using PcAgent.Tui.Rendering;

using Spectre.Console;

// /health : 健全性の概況。
public sealed class HealthCommand(SnapshotBuilder builder, RuleEngine engine) : ISlashCommand
{
    public string Name => "health";

    public string Description => "健全性の概況を表示";

    public string? ArgumentHint => null;

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var report = await DiagnosisExecutor.RunAsync(builder, engine, cancellationToken);
        DiagnosisRenderer.RenderSummary(report);
    }
}

// /diagnose : 全体診断。
public sealed class DiagnoseSlashCommand(SnapshotBuilder builder, RuleEngine engine) : ISlashCommand
{
    public string Name => "diagnose";

    public string Description => "診断を実行して指摘を表示";

    public string? ArgumentHint => null;

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var report = await DiagnosisExecutor.RunAsync(builder, engine, cancellationToken);
        DiagnosisRenderer.Render(report);
    }
}

// /report : 診断レポート(save で JSON 保存)。
public sealed class ReportCommand(SnapshotBuilder builder, RuleEngine engine) : ISlashCommand
{
    public string Name => "report";

    public string Description => "診断レポートを生成(save で保存)";

    public string? ArgumentHint => "[save]";

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var report = await DiagnosisExecutor.RunAsync(builder, engine, cancellationToken);
        DiagnosisRenderer.Render(report);

        if (String.Equals(arguments.Trim(), "save", StringComparison.OrdinalIgnoreCase))
        {
            var path = "diagnosis-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";
            await File.WriteAllTextAsync(path, DiagnosisJson.Serialize(report), cancellationToken);
            var saved = $"[silver]saved: {Markup.Escape(path)}[/]";
            AnsiConsole.MarkupLine(saved);
        }
    }
}

// /rules : ルール/閾値の状態(reload で再読込)。
public sealed class RulesCommand(RuleEngine engine, IOptions<DiagnosticsOptions> options) : ISlashCommand
{
    public string Name => "rules";

    public string Description => "ルール/閾値の状態を表示";

    public string? ArgumentHint => "[reload]";

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var (thresholds, rules) = engine.Describe();
        var o = options.Value;

        var l1 = $"[white]rules[/]      : [aqua]{rules.ToString(CultureInfo.InvariantCulture)}[/]  [silver]{Markup.Escape(o.RulesPath)}[/]";
        AnsiConsole.MarkupLine(l1);
        var l2 = $"[white]thresholds[/] : [aqua]{thresholds.ToString(CultureInfo.InvariantCulture)}[/]  [silver]{Markup.Escape(o.ThresholdsPath)}[/]";
        AnsiConsole.MarkupLine(l2);
        var note = "[silver](実行のたびに再読込されるため、ファイル変更は即反映されます)[/]";
        AnsiConsole.MarkupLine(note);
        return ValueTask.CompletedTask;
    }
}
