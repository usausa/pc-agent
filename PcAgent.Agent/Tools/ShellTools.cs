namespace PcAgent.Agent.Tools;

using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Options;

using PcAgent.Diagnostics.Options;

// LLM が起動するシェルツール。承認(ApprovalRequiredAIFunction)に加え、許可リストと
// シェル演算子の禁止で実行可能コマンドを制限する。ユーザー起動の ! とは別系統。
public sealed class ShellTools(IOptions<ActionsOptions> options)
{
    private const int MaxOutputChars = 4000;

    // パイプ/リダイレクト/連結など、許可リストを回避しうるシェル演算子。
    private static readonly SearchValues<char> ForbiddenChars = SearchValues.Create("&|<>^();`%\n\r");

    [Description("Windows の診断コマンドを実行し、その出力を返します。実行には承認が必要で、許可リストにあるコマンドのみ実行できます。")]
    public async Task<string> RunShellCommand(
        [Description("実行するコマンド(例: ipconfig /all)。先頭の語が許可リストにある必要があり、パイプ/リダイレクトは使用不可。")] string command,
        CancellationToken cancellationToken)
    {
        var actions = options.Value;

        if (!actions.AllowShell)
        {
            return "シェル実行は無効です(Actions:AllowShell=false)。";
        }

        if (String.IsNullOrWhiteSpace(command))
        {
            return "コマンドが空です。";
        }

        if (command.AsSpan().IndexOfAny(ForbiddenChars) >= 0)
        {
            return "コマンドにシェル演算子(& | < > 等)は使用できません。単一コマンドのみ実行可能です。";
        }

        var name = FirstToken(command);
        var allowed = actions.Shell.AllowedCommands;
        if (!allowed.Any(c => String.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
        {
            return String.Create(
                CultureInfo.InvariantCulture,
                $"コマンド '{name}' は許可リストにありません。許可: {String.Join(", ", allowed)}");
        }

        return await ExecuteAsync(command, actions.Shell.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
    }

    private static string FirstToken(string command)
    {
        var trimmed = command.TrimStart();
        var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? trimmed : trimmed[..space];
    }

    private static async Task<string> ExecuteAsync(string command, int timeoutSeconds, CancellationToken cancellationToken)
    {
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
            return "コマンドを起動できませんでした。";
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutSeconds > 0)
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        }

        // 両方のパイプをリダイレクトしているため、両方を読み進める必要がある。
        // stderr を読まないままにすると、そのバッファが埋まった時点で子プロセスが
        // 書き込みでブロックし、stdout も EOF にならず相互に待ち合う。
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);

        string output;
        string error;
        try
        {
            var streams = await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            output = streams[0];
            error = streams[1];
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            return String.Create(
                CultureInfo.InvariantCulture,
                $"$ {command}\nコマンドが {timeoutSeconds} 秒以内に終了しなかったため中断しました。");
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"$ {command} (exit {process.ExitCode})");
        if (output.Length > 0)
        {
            builder.Append('\n').Append(output);
        }

        if (error.Length > 0)
        {
            builder.Append("\n[stderr] ").Append(error);
        }

        var text = builder.ToString();
        return text.Length <= MaxOutputChars ? text : text[..MaxOutputChars] + "\n…(truncated)";
    }

    // cmd.exe /c 経由なので、子孫まで落とさないとコマンド本体が残る。
    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // すでに終了している
        }
        catch (Win32Exception)
        {
            // 落とせない場合は諦める
        }
    }
}
