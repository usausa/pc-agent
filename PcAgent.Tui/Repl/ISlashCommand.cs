namespace PcAgent.Tui.Repl;

// スラッシュコマンド。実装を DI に登録するだけで /コマンド が増える。
public interface ISlashCommand
{
    // コマンド名(先頭の / を除く)。
    string Name { get; }

    // 説明。
    string Description { get; }

    // 引数ヒント(例: "<category>")。無ければ null。
    string? ArgumentHint { get; }

    // 実行する。
    ValueTask ExecuteAsync(SlashCommandContext context, string arguments, CancellationToken cancellationToken);
}

// スラッシュコマンドの実行コンテキスト。
public sealed class SlashCommandContext
{
    // 登録済みコマンド一覧(/help 用)。
    public required IReadOnlyList<ISlashCommand> Commands { get; init; }

    // 終了が要求されたか。
    public bool ExitRequested { get; set; }

    // 処理した入力件数(終了サマリ用)。
    public int Inputs { get; set; }

    // 診断で発見した指摘件数(Warning 以上、終了サマリ用)。
    public int Findings { get; set; }
}
