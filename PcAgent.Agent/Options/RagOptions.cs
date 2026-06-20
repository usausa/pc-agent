namespace PcAgent.Agent.Options;

// RAG(ナレッジ検索)に関する設定。
public sealed class RagOptions
{
    // 設定セクション名。
    public const string SectionName = "Rag";

    // RAG 文脈注入を有効にするか。
    public bool Enabled { get; set; } = true;

    // ナレッジ格納ディレクトリ。
    public string KnowledgePath { get; set; } = "knowledge";
}
