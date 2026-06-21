namespace PcAgent.Tui.Repl;

using System.Globalization;

using Microsoft.Extensions.Options;

using PcAgent.Agent;
using PcAgent.Agent.Options;
using PcAgent.Diagnostics.Collectors;
using PcAgent.Diagnostics.Platform;
using PcAgent.Tui.Options;
using PcAgent.Tui.Rendering;

using Spectre.Console;

// /info : PC情報を表示。
public sealed class InfoSlashCommand(IEnumerable<ICollector> collectors) : ISlashCommand
{
    public string Name => "info";

    public string Description => "PC情報を表示";

    public string? ArgumentHint => "<category>";

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        await InfoExecutor.RunAsync(collectors, arguments, cancellationToken);
    }
}

// /status : セッション概況。
public sealed class StatusCommand(IAgentConversation conversation, IEnumerable<ICollector> collectors) : ISlashCommand
{
    public string Name => "status";

    public string Description => "セッションの概況を表示";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var configured = conversation.IsConfigured ? "[green]yes[/]" : "[yellow]no[/]";
        var admin = AdminChecker.IsAdministrator() ? "[green]yes[/]" : "[yellow]no[/]";
        var count = collectors.Count().ToString(CultureInfo.InvariantCulture);

        var l1 = $"[white]model[/]      : [aqua]{Markup.Escape(conversation.ModelName)}[/]";
        AnsiConsole.MarkupLine(l1);
        var l2 = $"[white]configured[/] : {configured}";
        AnsiConsole.MarkupLine(l2);
        var l3 = $"[white]admin[/]      : {admin}";
        AnsiConsole.MarkupLine(l3);
        var l4 = $"[white]collectors[/] : [aqua]{count}[/]";
        AnsiConsole.MarkupLine(l4);
        return ValueTask.CompletedTask;
    }
}

// /config : 現在の設定。
public sealed class ConfigCommand(IOptions<UiOptions> ui, IOptions<LlmOptions> llm) : ISlashCommand
{
    public string Name => "config";

    public string Description => "現在の設定を表示";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var u = ui.Value;
        var l = llm.Value;
        var apiKey = String.IsNullOrEmpty(l.ApiKey) ? "(unset)" : "****";
        var endpoint = String.IsNullOrEmpty(l.Endpoint) ? "(unset)" : l.Endpoint;
        var model = String.IsNullOrEmpty(l.Model) ? "(unset)" : l.Model;

        string[] lines =
        [
            $"[white]Llm:Provider[/]        : [aqua]{l.Provider}[/]",
            $"[white]Llm:Model[/]           : [aqua]{Markup.Escape(model)}[/]",
            $"[white]Llm:Endpoint[/]        : [aqua]{Markup.Escape(endpoint)}[/]",
            $"[white]Llm:ApiKey[/]          : [aqua]{apiKey}[/]",
            $"[white]Ui:DecorationLevel[/]  : [aqua]{u.DecorationLevel}[/]",
            $"[white]Ui:CompletionEngine[/] : [aqua]{u.CompletionEngine}[/]",
        ];

        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine(line);
        }

        return ValueTask.CompletedTask;
    }
}

// /doctor : 自己診断。
public sealed class DoctorCommand(IAgentConversation conversation, IEnumerable<ICollector> collectors) : ISlashCommand
{
    public string Name => "doctor";

    public string Description => "自己診断(接続/権限/コレクタ)";

    public string? ArgumentHint => null;

    public ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var header = "[bold aqua]:stethoscope:  self-check[/]";
        AnsiConsole.MarkupLine(header);

        var configured = conversation.IsConfigured;
        Check("LLM 接続", configured, configured ? conversation.ModelName : "未設定 (Llm:* を設定)");

        var admin = AdminChecker.IsAdministrator();
        Check("管理者権限", admin, admin ? "あり" : "なし (温度/電力/SMART は制限)");

        var count = collectors.Count();
        Check("情報コレクタ", count > 0, count.ToString(CultureInfo.InvariantCulture) + " 個");

        return ValueTask.CompletedTask;
    }

    private static void Check(string name, bool ok, string detail)
    {
        var icon = ok ? "[green]:check_mark:[/]" : "[yellow]:warning:[/]";
        var line = $"  {icon} [white]{Markup.Escape(name)}[/]: [silver]{Markup.Escape(detail)}[/]";
        AnsiConsole.MarkupLine(line);
    }
}
