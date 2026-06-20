namespace PcAgent.Agent;

// 1つの対話セッション。応答をイベントストリームとして返す。実装を差し替えれば擬似/実 LLM を切り替えられる。
public interface IAgentConversation
{
    // エージェントの表示名。
    string AgentName { get; }

    // モデルの表示名。
    string ModelName { get; }

    // LLM 接続が構成済みか。
    bool IsConfigured { get; }

    // 利用者メッセージを送り、応答イベントを非同期ストリームで受け取る。
    IAsyncEnumerable<AgentEvent> SendAsync(string userMessage, CancellationToken cancellationToken = default);
}
