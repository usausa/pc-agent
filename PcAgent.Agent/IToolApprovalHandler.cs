namespace PcAgent.Agent;

// ツール実行の承認(HITL)をユーザーに問う。TUI が実装を提供する。
public interface IToolApprovalHandler
{
    // 承認する場合 true。
    ValueTask<bool> ApproveAsync(string toolName, string arguments, CancellationToken cancellationToken);
}
