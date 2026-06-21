namespace PcAgent.Tui.Repl;

using PcAgent.Agent;

using Spectre.Console;

// LLM のツール実行承認を、コンソールで y/N 確認するハンドラ。
public sealed class ConsoleApprovalHandler : IToolApprovalHandler
{
    public ValueTask<bool> ApproveAsync(string toolName, string arguments, CancellationToken cancellationToken)
    {
        var prompt = $"[bold yellow]ツール '{Markup.Escape(toolName)}' ({Markup.Escape(arguments)}) を承認しますか? (y/N): [/]";
        AnsiConsole.Markup(prompt);
        var answer = Console.ReadLine()?.Trim();
        var approved = String.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) || String.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
        return ValueTask.FromResult(approved);
    }
}
