namespace PcAgent.Agent.Rag;

using System.IO;

// knowledge/ 配下のポリシー文書を読み込み、簡易検索する(実運用ではベクタ検索に置換)。
internal sealed class KnowledgeStore
{
    private readonly List<KnowledgeDocument> documents;

    public KnowledgeStore(string knowledgePath)
    {
        documents = Load(knowledgePath);
    }

    public int Count => documents.Count;

    public IEnumerable<KnowledgeDocument> Search(string query)
    {
        var terms = Tokenize(query);
        var hits = documents
            .Where(d => terms.Any(t => d.Text.Contains(t, StringComparison.OrdinalIgnoreCase) || d.Source.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return hits.Count > 0 ? hits : documents;
    }

    private static List<KnowledgeDocument> Load(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        var documents = new List<KnowledgeDocument>();
        foreach (var file in Directory.EnumerateFiles(path, "*.md").Concat(Directory.EnumerateFiles(path, "*.txt")))
        {
            try
            {
                documents.Add(new KnowledgeDocument(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file)));
            }
            catch (IOException)
            {
                // 読み取り不可はスキップ。
            }
        }

        return documents;
    }

    private static IReadOnlyList<string> Tokenize(string query) =>
        [.. query
            .Split([' ', '\t', '\n', '、', '。', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static t => t.Length >= 2)];
}

// ナレッジ 1 文書。Source は出典名(ファイル名)。
internal readonly record struct KnowledgeDocument(string Source, string Text);
