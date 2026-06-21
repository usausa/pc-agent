namespace PcAgent.Tui.Repl;

using Spectre.Console;

// REPL の 1 行入力を抽象化する。
internal interface IInputReader
{
    // 次の入力行を返す。EOF(Ctrl+Z / パイプ終端)では null。
    Task<string?> ReadLineAsync(CancellationToken cancellationToken);
}

// 素の Console.ReadLine ベースの入力。リダイレクト/非対話でも動作する。
internal sealed class BuiltinInputReader : IInputReader
{
    public Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var prompt = "[bold green]>[/] ";
        AnsiConsole.Markup(prompt);
        var line = Console.ReadLine();
        return Task.FromResult(line);
    }
}
