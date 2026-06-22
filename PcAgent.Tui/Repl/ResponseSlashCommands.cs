namespace PcAgent.Tui.Repl;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using Spectre.Console;

// /copy : 直近のエージェント応答をクリップボードへコピー(clip.exe / UTF-16LE)。
public sealed class CopyCommand(LastResponseStore store) : ISlashCommand
{
    public string Name => "copy";

    public string Description => "直近のエージェント応答をクリップボードにコピー";

    public string? ArgumentHint => null;

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var text = store.Text;
        if (String.IsNullOrWhiteSpace(text))
        {
            AnsiConsole.MarkupLine("[yellow]コピーする応答がありません。[/]");
            return;
        }

        try
        {
            var info = new ProcessStartInfo("clip.exe")
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = new UnicodeEncoding(false, false),
            };

            using var process = Process.Start(info);
            if (process is null)
            {
                AnsiConsole.MarkupLine("[yellow]クリップボードへコピーできませんでした。[/]");
                return;
            }

            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);

            var count = text.Length.ToString(CultureInfo.InvariantCulture);
            AnsiConsole.MarkupLine($"[silver]直近の応答をクリップボードにコピーしました([aqua]{count}[/] 文字)。[/]");
        }
        catch (Win32Exception)
        {
            AnsiConsole.MarkupLine("[yellow]clip.exe を起動できませんでした。[/]");
        }
    }
}

// /save : 直近のエージェント応答をファイルへ保存(引数でパス指定可。既定はタイムスタンプ名)。
public sealed class SaveCommand(LastResponseStore store) : ISlashCommand
{
    public string Name => "save";

    public string Description => "直近のエージェント応答をファイルに保存";

    public string? ArgumentHint => "[path]";

    public async ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken)
    {
        var text = store.Text;
        if (String.IsNullOrWhiteSpace(text))
        {
            AnsiConsole.MarkupLine("[yellow]保存する応答がありません。[/]");
            return;
        }

        try
        {
            var path = ResolvePath(arguments.Trim());
            var directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false), cancellationToken);
            AnsiConsole.MarkupLine($"[silver]応答を保存しました: [aqua]{Markup.Escape(path)}[/][/]");
        }
        catch (IOException ex)
        {
            Fail(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            Fail(ex);
        }
        catch (ArgumentException ex)
        {
            Fail(ex);
        }

        static void Fail(Exception ex) => AnsiConsole.MarkupLine($"[yellow]保存に失敗しました: {Markup.Escape(ex.Message)}[/]");
    }

    private static string ResolvePath(string argument)
    {
        if (argument.Length > 0)
        {
            return Path.GetFullPath(argument);
        }

        var name = "pcagent-response-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".md";
        return Path.GetFullPath(name);
    }
}
