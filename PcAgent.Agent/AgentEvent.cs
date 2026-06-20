namespace PcAgent.Agent;

// エージェント応答中に流れるストリーミングイベント。UI 非依存で、各 UI はこれを購読して描画する。
public abstract record AgentEvent;

// 思考(reasoning)フェーズの開始。
public sealed record ThinkingStarted : AgentEvent;

// 思考内容の断片。
public sealed record ThinkingDelta(string Text) : AgentEvent;

// 思考フェーズの終了。
public sealed record ThinkingCompleted : AgentEvent;

// ツール(関数)呼び出しの開始。
public sealed record ToolCallStarted(string Name, string Arguments) : AgentEvent;

// ツール呼び出しの完了。
public sealed record ToolCallCompleted(string Name, string Result) : AgentEvent;

// 本文テキストの断片(ストリーミング)。
public sealed record TextDelta(string Text) : AgentEvent;

// 応答全体の完了。
public sealed record ResponseCompleted : AgentEvent;
