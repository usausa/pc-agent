namespace PcAgent.Agent.Options;

// LLM プロバイダーの選択肢。
public enum LlmProvider
{
    // Microsoft Foundry / Azure OpenAI。
    Foundry,

    // Ollama(OpenAI 互換エンドポイント)。
    Ollama,

    // Foundry Local。
    FoundryLocal,
}

// LLM 接続に関する設定。
public sealed class LlmOptions
{
    // 設定セクション名。
    public const string SectionName = "Llm";

    // 使用するプロバイダー。
    public LlmProvider Provider { get; set; } = LlmProvider.Foundry;

    // エンドポイント。
    public string? Endpoint { get; set; }

    // API キー。
    public string? ApiKey { get; set; }

    // モデル(デプロイメント)名。
    public string? Model { get; set; }

    // コンテキストウィンドウのトークン数(/context の使用率算出に使用)。
    public int ContextWindow { get; set; } = 128000;
}
