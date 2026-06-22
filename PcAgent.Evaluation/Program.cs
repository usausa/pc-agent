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

// ローカル検査のみ: 応答が非空 / GetPcInfo ツールを呼ぶ。
var evaluator = new LocalEvaluator(
[
    EvalChecks.NonEmpty(1),
    EvalChecks.ToolCalledCheck("GetPcInfo"),
]);

string[] queries =
[
    "CPUの温度と使用率を教えて。",
    "メモリの空き容量は?",
    "システム情報(OS とマシン名)を教えて。",
];

await Console.Out.WriteLineAsync("=== PcAgent 評価 (LocalEvaluator) ===").ConfigureAwait(false);
await Console.Out.WriteLineAsync("検査: 応答が非空 / GetPcInfo ツールを呼ぶ").ConfigureAwait(false);
foreach (var query in queries)
{
    await Console.Out.WriteLineAsync("  - " + query).ConfigureAwait(false);
}

var results = await agent.EvaluateAsync(queries, evaluator).ConfigureAwait(false);

await Console.Out.WriteLineAsync().ConfigureAwait(false);
await Console.Out.WriteLineAsync(String.Create(CultureInfo.InvariantCulture, $"合格項目: {results.Passed} / {results.Total}")).ConfigureAwait(false);
await Console.Out.WriteLineAsync("全合格  : " + (results.AllPassed ? "はい" : "いいえ")).ConfigureAwait(false);

return results.AllPassed ? 0 : 1;
