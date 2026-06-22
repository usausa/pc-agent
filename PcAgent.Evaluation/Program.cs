// PcAgent エージェントの品質評価(LocalEvaluator)。CI/開発時に実行する。
// 代表的な質問でエージェントを走らせ、ローカル検査(追加 LLM 呼び出しなし)で採点する。
// 実行には LLM 認証情報が必要(Llm:Endpoint / ApiKey / Model。ユーザーシークレット pc-agent を共有)。
using System.Globalization;
using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenAI.Chat;

using PcAgent.Agent;
using PcAgent.Agent.Options;
using PcAgent.Agent.Tools;
using PcAgent.Diagnostics;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables()
    .Build();

// コレクタ → 情報取得ツール。
var services = new ServiceCollection();
services.AddDiagnostics(config);
services.AddSingleton<PcInfoTools>();
using var provider = services.BuildServiceProvider();
var pcTools = provider.GetRequiredService<PcInfoTools>();

var llm = config.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
var chatClient = ChatClientFactory.TryCreate(llm);
if (chatClient is null)
{
    await Console.Error.WriteLineAsync("LLM 未設定です(Llm:Endpoint / ApiKey / Model)。評価を実行できません。").ConfigureAwait(false);
    return 2;
}

// 評価用エージェント(情報取得ツールのみ)。
var agentOptions = new ChatClientAgentOptions
{
    Name = "PcAgentEval",
    ChatOptions = new ChatOptions
    {
        Instructions =
            "あなたはこの Windows PC の状態を調べるアシスタントです。" +
            "質問にはツールで実際の値を取得し、簡潔に日本語で答えてください。",
        Tools =
        [
            AIFunctionFactory.Create(pcTools.GetPcInfo),
            AIFunctionFactory.Create(pcTools.ListCategories),
        ],
    },
};
var agent = chatClient.AsAIAgent(agentOptions);

// ケースごとに観点別の検査を割り当てる(質問ごとに評価器を分ける)。
// 観点: 接地(ツールで実値取得=値を捏造しない) / 正しいツール選択 / 応答内容の妥当性(OS名)。
var cases = new (string Title, string Query, LocalEvaluator Evaluator)[]
{
    ("OS情報: 接地 + 応答に 'Windows' (GetPcInfo + KeywordCheck)", "このPCのOS名を教えて。",
        new LocalEvaluator([EvalChecks.NonEmpty(1), EvalChecks.ToolCalledCheck("GetPcInfo"), EvalChecks.KeywordCheck("Windows")])),
    ("CPU情報: 接地 (GetPcInfo)", "CPUの温度と使用率を教えて。",
        new LocalEvaluator([EvalChecks.NonEmpty(1), EvalChecks.ToolCalledCheck("GetPcInfo")])),
    ("メモリ情報: 接地 (GetPcInfo)", "メモリの空き容量は?",
        new LocalEvaluator([EvalChecks.NonEmpty(1), EvalChecks.ToolCalledCheck("GetPcInfo")])),
    ("ツール選択: 一覧質問 → ListCategories", "このエージェントが取得できる情報カテゴリの一覧を、ツールを使って教えて。",
        new LocalEvaluator([EvalChecks.NonEmpty(1), EvalChecks.ToolCalledCheck("ListCategories")])),
};

await Console.Out.WriteLineAsync("=== PcAgent 評価 (LocalEvaluator) ===").ConfigureAwait(false);

var passed = 0;
foreach (var (title, query, evaluator) in cases)
{
    var result = await agent.EvaluateAsync([query], evaluator).ConfigureAwait(false);
    passed += result.AllPassed ? 1 : 0;
    await Console.Out.WriteLineAsync((result.AllPassed ? "✅ " : "❌ ") + title).ConfigureAwait(false);
}

await Console.Out.WriteLineAsync().ConfigureAwait(false);
await Console.Out.WriteLineAsync(String.Create(CultureInfo.InvariantCulture, $"合格: {passed} / {cases.Length}")).ConfigureAwait(false);
await Console.Out.WriteLineAsync("全合格: " + (passed == cases.Length ? "はい" : "いいえ")).ConfigureAwait(false);

return passed == cases.Length ? 0 : 1;
