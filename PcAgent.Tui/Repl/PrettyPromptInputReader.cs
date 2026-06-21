namespace PcAgent.Tui.Repl;

using PrettyPrompt;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;

// PrettyPrompt ベースの入力。/ と @ のポップアップ補完を提供する(対話端末用)。
internal sealed class PrettyPromptInputReader(IReadOnlyList<string> slashNames, IReadOnlyList<string> sourceNames) : IInputReader, IAsyncDisposable
{
    private readonly Prompt prompt = new(callbacks: new ReplPromptCallbacks(slashNames, sourceNames));

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var result = await prompt.ReadLineAsync();
        return result.IsSuccess ? result.Text : null;
    }

    public ValueTask DisposeAsync() => prompt.DisposeAsync();
}

// / と @ の補完候補を提供するコールバック。
internal sealed class ReplPromptCallbacks(IReadOnlyList<string> slashNames, IReadOnlyList<string> sourceNames) : PromptCallbacks
{
    protected override Task<TextSpan> GetSpanToReplaceByCompletionAsync(string text, int caret, CancellationToken cancellationToken)
    {
        var start = 0;
        for (var i = caret - 1; i >= 0; i--)
        {
            if (text[i] == ' ')
            {
                start = i + 1;
                break;
            }
        }

        return Task.FromResult(new TextSpan(start, caret - start));
    }

    protected override Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
    {
        var typed = caret >= 1 && caret <= text.Length ? text[1..caret] : String.Empty;
        var hasSpace = text.Contains(' ', StringComparison.Ordinal);

        if (text.StartsWith('/') && !hasSpace)
        {
            return Task.FromResult(Build(slashNames, '/', typed));
        }

        if (text.StartsWith('@') && !hasSpace)
        {
            return Task.FromResult(Build(sourceNames, '@', typed));
        }

        return Task.FromResult<IReadOnlyList<CompletionItem>>([]);
    }

    protected override Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        var hasSpace = text.Contains(' ', StringComparison.Ordinal);
        var open = text.Length > 0 && (text[0] == '/' || text[0] == '@') && !hasSpace;
        return Task.FromResult(open);
    }

    private static IReadOnlyList<CompletionItem> Build(IReadOnlyList<string> names, char prefix, string typed) =>
    [
        .. names
            .Where(n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .Select(n => new CompletionItem(prefix + n, displayText: prefix + n)),
    ];
}
