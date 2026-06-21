namespace PcAgent.Tui.Repl;

using System.Diagnostics;

using Microsoft.Extensions.Options;

using PcAgent.Diagnostics.Options;

using Spectre.Console;

// ! コマンドのシェル実行。ユーザー起動のため直接実行し、出力を取り込む。Actions:AllowShell で制御。
public sealed class ShellRunner(IOptions<ActionsOptions> options)
{
    public async Task RunAsync(string command, CancellationToken cancellationToken)
    {
        if (String.IsNullOrWhiteSpace(command))
        {
            return;
        }

        if (!options.Value.AllowShell)
        {
            var disabled = "[yellow]シェル実行は無効です(Actions:AllowShell=false)。[/]";
            AnsiConsole.MarkupLine(disabled);
            return;
        }

        var startInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return;
        }

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (standardOutput.Length > 0)
        {
            AnsiConsole.Write(new Text(standardOutput));
        }

        if (standardError.Length > 0)
        {
            AnsiConsole.Write(new Text(standardError, new Style(foreground: Color.Red)));
        }
    }
}
