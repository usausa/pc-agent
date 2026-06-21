# PC 診断・情報取得エージェント 実装プラン

> ステータス: **Plan v1.0**
> 対象仕様: [`docs/spec.md`](spec.md)（Draft v0.3）
> 作成日: 2026-06-20

本書は仕様 v0.3 を実装に落とすためのフェーズ分割・チェックリスト・完了基準（DoD）をまとめたもの。各フェーズは**独立して「警告ゼロでビルド可能」**な状態を保ちながら積み上げる。

---

## 0. 進め方と全体方針

### 開発原則

- **各フェーズ末で警告ゼロ**（`AnalysisMode=All` / `WarningsAsErrors=nullable`）。抑制が要る場合は**事前相談**（AGENTS.md）。
- **ストリーミング基本**（`RunStreamingAsync`）、**決定的処理はルール（外部 JSON）**、**UI は AgentEvent で疎結合**。
- **装飾は罫線不使用・絵文字/進捗グラフ使用・日本語優先**。
- 既存参考コードからの流用を優先（再発明しない）。

### 参照元（どこから流用するか）

| 領域 | 参照元 |
| --- | --- |
| CLI 基盤・単発コマンド雛形 | `D:\GitHub\Smart-Net-CommandLine`（Develop）/ `D:\GitHubDevice\tool-bt-tool\BleScan` |
| HW センサー・SMART 収集 | `D:\GitHubDevice\Service-PrometheusExporter` |
| Agent Framework 配線（ツール/ミドルウェア/RAG/承認/テレメトリ/構造化/ストリーミング） | `D:\GitHub\Work-Net\WorkAgent`（AgentSampleCore / Feature01〜14） |
| TUI（Spectre・AgentEvent・CJK 対策） | `D:\GitHub\Work-Net\WorkAgent\TuiSpectreConsoleAgentSample` / `TuiAgentSampleCore` |

### フェーズ依存と順序

```
P0 基盤
 └─> P1 CLI基盤 ──> P2 情報取得 ──> P3 エージェント+ストリーミング ──> P4 TUI+Spectre検証(PoC)
                                                                          │
                          ┌───────────────────────────────────────────────┘
                          ▼
                       P5 REPL/スラッシュ/サジェスト ──> P6 診断(外部ルール) ──> P7 RAG/Context
                                                                          │
                          ┌───────────────────────────────────────────────┘
                          ▼
                       P8 HITLアクション ──> P9 可観測性(OTEL) ──> P10 カスタムコマンド+仕上げ
```

> **PoC の前倒し**: §9 の Spectre 検証（特に C1 ビューポート超過・C7 補完協調）は、擬似ストリームを使って **P1 後のスパイク**として先行実施してもよい（表示方式の決定を早めるため）。

### マイルストーン

| MS | 範囲 | 到達状態 |
| --- | --- | --- |
| **M1** | P0–P2 | `pcagent info <category>` で実機の HW/SMART/システム情報が表示される |
| **M2** | P3–P4 | 質問→ストリーミング回答（ツール呼び出し可視化）。本文表示方式・補完方式を確定 |
| **M3** | P5 | `/コマンド`・`@`注入・`!`シェル・サジェスト・割り込みが動く |
| **M4** | P6 | 外部 JSON ルール/閾値で診断が走り、ダッシュボード表示 |
| **M5** | P7–P8 | RAG 注入 + HITL 承認付きアクション（一時/bin・obj 削除） |
| **M6** | P9–P10 | OTEL/OTLP（既定オフ）・カスタムコマンド・単一ファイル発行 |

### 横断的 Definition of Done（全フェーズ共通）

- [ ] 警告ゼロでビルド（`dotnet build`）
- [ ] `.editorconfig` 準拠・メンバ変数に `_` プレフィックスなし
- [ ] 数値整形は `CultureInfo.InvariantCulture` + `string.Create`
- [ ] 公開 API/設定キーは仕様（§12）と整合
- [ ] 管理者権限が要る箇所は非管理者でも例外でなく縮退

---

## フェーズ 0: リポジトリ／ソリューション基盤

**目的**: 3 プロジェクト構成と共通ビルド設定を用意し、空ビルドが通る状態にする。

**主な成果物**: `PcAgent.slnx` 更新、`PcAgent.Diagnostics` / `PcAgent.Agent` / `PcAgent.Tui` の空プロジェクト、TFM・参照の骨格。

**参照**: 既存 `Directory.Build.props` / BleScan の csproj・GlobalSuppressions・publish プロファイル。

**チェックリスト**
- [x] `PcAgent.Diagnostics`（classlib, `net10.0-windows10.0.26100.0`）作成
- [x] `PcAgent.Agent`（classlib, 同 TFM）作成
- [x] `PcAgent.Tui`（exe, 同 TFM）作成、参照: Tui→Agent→Diagnostics
- [x] `PcAgent.slnx` に 3 プロジェクト追加
- [x] 既存 `Directory.Build.props` 継承を確認 + 各 csproj から `Analyzers.ruleset` を参照
- [x] CS ファイルは UTF-8（BOMなし）+ CRLF を確認
- [~] `GlobalSuppressions.cs`：P0 はコードが無く不要。必要時に AGENTS.md 準拠で相談のうえ追加
- [~] publish プロファイル：**P10 へ繰り延べ**（本リポジトリは `*.pubxml` が gitignore 対象・ルートに `publish.bat` あり）
- [x] `.gitignore` に bin/obj 等が含まれることを確認

**完了基準**: 3 プロジェクトが警告ゼロで空ビルド成功。✅ **達成**（0 警告 / 0 エラー、`PcAgent.Tui` 起動 ExitCode=0、UTF-8 BOMなし+CRLF 確認済み）。

---

## フェーズ 1: CLI 基盤（Smart.CommandLine.Hosting）

**目的**: `CommandHost` でホスト・DI・設定・フィルタを立ち上げ、単発コマンドの骨格を作る。

**主な成果物**: `Program.cs`、ルートコマンド（REPL 入口）、`info`/`diagnose` のスケルトン、グローバルフィルタ、設定 POCO。

**主要型（案）**
```csharp
// PcAgent.Tui/Program.cs
var builder = CommandHost.CreateBuilder(args).UseDefaults();
builder.Services.AddDiagnostics();   // P2 で実装する拡張メソッド（DI 登録）
builder.Services.AddPcAgent();       // P3
builder.ConfigureCommands(commands =>
{
    commands.AddGlobalFilter<ExecutionTimeFilter>(order: -100);
    commands.AddGlobalFilter<ExceptionHandlingFilter>(order: int.MaxValue);
    commands.ConfigureRootCommand(root => root.WithDescription("PC diagnostics agent").UseHandler<RootCommandHandler>());
    commands.AddCommand<InfoCommand>();
    commands.AddCommand<DiagnoseCommand>();
});
return await builder.Build().RunAsync();

// RootCommandHandler : ICommandHandler  … 引数なし→REPL、--ask 指定→単発
// InfoCommand / DiagnoseCommand : ICommandHandler  … 非対話用
// ExecutionTimeFilter / ExceptionHandlingFilter : ICommandFilter
// Options: LlmOptions, CollectionOptions, DiagnosticsOptions, ActionsOptions, TelemetryOptions, UiOptions（§12）
```

**参照**: BleScan `Program.cs`（`ConfigureRootCommand`+`UseHandler`）、Develop の `ExecutionTimeFilter`/`ExceptionHandlingFilter`。

**チェックリスト**
- [x] `Usa.Smart.CommandLine.Hosting` 2.8.0 参照（`System.CommandLine` は推移参照のため明示不要）
- [x] `Program.cs`：`CreateBuilder().UseDefaults()`（appsettings/env/user-secrets）
- [x] `RootCommandHandler`：引数なしで REPL プレースホルダ、`--ask <q>` で単発プレースホルダ
- [x] `InfoCommand`（`info --category <cat> [--json]`）スケルトン ※ positional 未対応のため `--category` オプション
- [x] `DiagnoseCommand`（`diagnose [--category] [--json]`）スケルトン
- [x] `ExecutionTimeFilter`（実行時間ログ）+ `CancellationFilter`（取り消し処理）※ 全体例外整形は P9 で拡張
- [x] 設定 POCO 群（§12 の scalar 群）を `Configure<T>` でバインド ※ 配列系(BinObj/Enabled 等)は消費フェーズで追加
- [x] `appsettings.json` 雛形（Llm/Collection/Diagnostics/Actions/Telemetry/Ui）
- [x] `--help` 出力確認
- [x] `GlobalSuppressions.cs`（CA1515 / CA1848 / CA2007）で framework・console 都合の警告を抑制（BleScan / Example.Web と同方式）
- [x] CS ファイルは UTF-8（BOMなし）+ CRLF を確認
- [x] ソリューションを `PcAgent.slnx` / `PcAgent.sln.DotSettings` にリネーム

**完了基準**: `pcagent --help` / `info -c cpu` / `diagnose` / `--ask "test"` / 引数なし がすべて起動・ExitCode 0。✅ **達成**（0 警告 / 0 エラー）。

---

## フェーズ 2: 情報取得（Diagnostics / Collectors）

**目的**: 拡張可能なコレクタ層で実機の HW/SMART/システム情報を取得し、`info` で表示する。

**主な成果物**: `ICollector` 抽象、Hardware/Smart/System コレクタ、スナップショットモデル、DI 登録、管理者権限判定。

**主要型（案）**
```csharp
public enum CollectorCategory { Cpu, Gpu, Memory, Motherboard, Storage, Network, Battery, System, Smart /*, Wifi(将来)*/ }

public interface ICollector
{
    string Name { get; }                 // "cpu","disk","smart",...
    CollectorCategory Category { get; }
    ValueTask<CollectorResult> CollectAsync(CancellationToken ct);
}

// HardwareCollector : LibreHardwareMonitor（Computer + IVisitor + 節流更新）
// SmartCollector    : HardwareInfo.Disk（DiskInfo.GetInformation / NVMe・Generic）
// SystemCollector   : BCL（RuntimeInformation/DriveInfo/Process）
// Snapshot          : 各カテゴリの値（欠損は double.NaN）
// services.AddDiagnostics() で全 ICollector を DI 登録
```

**参照**: PrometheusExporter `HardwareMonitorInstrumentation`（初期化・Visitor・節流）、`DiskInfoInstrumentation`（NVMe/Generic）、`NativeMethods`（`GetPhysicallyInstalledSystemMemory`）、`PcTools`（システム情報・プロセス）。

**チェックリスト**
- [x] `LibreHardwareMonitorLib` 0.9.6 / `HardwareInfo.Disk` 1.12.0 参照（`AllowUnsafeBlocks`=true: LibraryImport 生成のため）
- [x] `ICollector` + `CollectorResult`/`MetricGroup`/`MetricValue` モデル定義
- [x] `HardwareMonitorSource`（`Computer` 初期化・`UpdateVisitor`・節流更新）+ `HardwareCollectorBase` + 7 カテゴリ派生
- [x] CPU/GPU/メモリ/MB/ストレージ/NW/バッテリーのメトリクス取得（センサー→`SensorKind`+単位で汎用変換）
- [x] `SmartCollector`：NVMe（PercentageUsed/Temperature/CriticalWarning/AvailableSpare 等）・Generic（属性 ID/RawValue、代表 ID は名称化）
- [x] `SystemCollector`：OS/アーキ/プロセッサ数/.NET/稼働時間/ドライブ/上位プロセス（`Process.Dispose` 防御、ドライブは `IsReady`+IO 例外ガード）
- [x] 欠損値の扱い統一 ※ JSON 互換のため `double.NaN` ではなく **`null`**（`double?`）に変更
- [x] `services.AddDiagnostics()`（`HardwareMonitorSource` + 9 コレクタ + オプション登録）
- [x] 管理者権限判定 `AdminChecker`（不足時は note 表示 + 取得可能範囲で縮退）
- [~] `Collection:UpdateIntervalMs`（節流に反映）。`SmartIntervalMs`/カテゴリ別 Enable は将来（現状は全 HW 有効・単発取得）
- [x] `InfoCommand` で実データ表示（プレーンテキスト + `--json`）。ログは stderr 分離（stdout=出力専用）

**完了基準**: `info system`（OS/メモリ/全ドライブ/プロセス）・`info cpu`（実コア負荷）・`info smart`（ディスク構造化）・`--json`（クリーン JSON）。✅ **達成**（0 警告 / 0 エラー、UTF-8 BOMなし+CRLF）。温度/電力/SMART 値は管理者実行時に充足。

---

## フェーズ 3: エージェント + ストリーミング

**目的**: 設定で選んだ `IChatClient` からエージェントを構築し、`RunStreamingAsync` を AgentEvent に変換して最小表示する。

**主な成果物**: `IChatClient` プロバイダーファクトリ（Foundry）、関数ツール、ストリーミングブリッジ、AgentEvent モデル。

**主要型（案）**
```csharp
public interface IChatClientFactory { IChatClient Create(LlmOptions options); }
// FoundryChatClientFactory: AzureOpenAIClient(Uri, ApiKeyCredential).GetChatClient(model).AsIChatClient()
//   将来 Ollama/FoundryLocal は OpenAI 互換エンドポイントで差し替え

// PcTools 相当: AIFunctionFactory.Create(collector ラッパー) を tools: に渡す
// AgentEvent: ThinkingStarted/Delta/Completed, ToolCallStarted/Completed, TextDelta, ResponseCompleted
// IAgentConversation.SendAsync(string): IAsyncEnumerable<AgentEvent>
// StreamingBridge: RunStreamingAsync の AgentResponseUpdate → AgentEvent（Contents の FunctionCallContent を検出）
```

**参照**: AgentSampleCore（接続・`AsAIAgent`）、Feature01（ツール）、Feature09（`RunStreamingAsync`/`AgentResponseUpdate`）、TuiAgentSampleCore（`AgentEvent`/`IAgentConversation`）。

**チェックリスト**
- [x] `Microsoft.Agents.AI` 1.10.0 / `.OpenAI` 1.10.0 / `Azure.AI.OpenAI` 2.1.0 参照
- [x] `LlmOptions`（Provider/Endpoint/ApiKey/Model）。設定は appsettings/user-secrets/環境変数(`Llm__*`) ※ `Foundry__*` 別名は未対応（`Llm` セクションに統一）
- [x] `ChatClientFactory.TryCreate`（Foundry=AzureOpenAIClient / Ollama・FoundryLocal=OpenAIClient+Endpoint。未設定なら null）
- [x] 接続未設定時の案内メッセージ（`IsConfigured` 判定 → 設定方法を表示・exit 1）
- [x] コレクタをラップした関数ツール `PcInfoTools`（`GetPcInfo(category)` 引数つき・`ListCategories()`、`[Description]` 付与）
- [x] エージェント生成（`chatClient.AsAIAgent(instructions, name, tools)`）
- [x] `AgentEvent` モデル + `IAgentConversation` / `PcAgentConversation`（実 LLM 版）
- [x] ストリーミング変換：`RunStreamingAsync` → AgentEvent（`FunctionCallContent`/`FunctionResultContent` 検出、テキスト断片）。classlib は `ConfigureAwait(false)`
- [x] `RootCommandHandler --ask` で 1 問ストリーミング表示（最小 Console・async I/O）

**完了基準**: `pcagent --ask "<質問>"` でツール呼び出し→ストリーミング回答。✅ **配線達成**（0 警告 / 0 エラー、DI 解決・未設定経路を確認）。実 LLM 往復は Foundry 等の接続情報設定時に動作（現環境は未設定のため未実行）。

---

## フェーズ 4: TUI 描画 + Spectre 検証（PoC）

**目的**: Spectre.Console での描画方式と PrettyPrompt 補完を**実機検証して確定**し、本描画を実装する。

**主な成果物**: §9 PoC レポート、確定した本文表示方式（Live or スクロールバック）と補完方式、レンダラ実装。

**主要型（案）**
```csharp
// IRenderer: AgentEvent を受けて描画（思考スピナー/ツールカード/本文/完了統計）
// ResponseState: Thinking/LastThought/Tools/Answer/Tokens/Completed
// MarkupFormatter（簡易 Markdown→Spectre、CJK 安全）/ ResponseTokenizer（CJK 1 文字分割）
// IInputReader: PrettyPrompt 実装 / Builtin 実装（フォールバック）
```

**参照**: TuiSpectreConsoleAgentSample（`Live`/`Apply`/`SpectreChatView`/リダイレクト分岐）、`MarkupFormatter`/`ResponseTokenizer`/`AgentBranding`。

**PoC（§9 C1–C7）チェックリスト**
- [x] C1〜C3: **Live を使わない設計**を採用し原理回避（本文=スクロールバック逐次出力）。※本環境は非対話のため Live 自体の実測は不要化
- [~] C4: 枠なし + 幅 1 ブロックバー構成で整列崩れを回避。絵文字(🖥️/⚙️)はリダイレクト出力でも描画確認。**実ターミナルでの最終目視はユーザー確認推奨**
- [x] C5: 本文は `Markup.Escape` で都度エスケープ（途中整形なし）
- [x] C6: 静的 `AnsiConsole` 描画でリダイレクト/ヘッドレス動作を確認
- [~] C7: PrettyPrompt は対話 REPL の要素のため **P5 で実機検証**（不可なら Builtin へ）
- [x] **結論を記録**：本文=スクロールバック・構造化=静的 Spectre に確定（[spec §9.4](spec.md)）

**本描画チェックリスト**
- [x] 起動バナー（`FigletText`・配色、罫線なし）
- [~] 思考スピナー / ツール呼び出し表示：ツール呼び出しは色付き行で表示。スピナー(`Status`)は対話 REPL（P5・リダイレクトガード付き）へ
- [x] 本文ストリーミング（スクロールバック逐次・`AnsiConsole.Markup`）
- [~] `MarkupFormatter` / `ResponseTokenizer`：生ストリーミングには不要のため見送り（Markdown 整形が必要になった段階で導入）
- [x] 配色ロール（白/aqua/silver/green/blue/yellow、`grey` 不使用）、`Markup.Escape` 徹底
- [x] `info` のリッチ描画（絵文字ヘッダ + 色 + 進捗バー `█░`）

**完了基準**: PoC 結論が文書化され表示方式が確定。`info`/バナーがリッチ表示（枠なし・絵文字・バー）で安定描画。✅ **達成**（0 警告 / 0 エラー、リダイレクトでも動作）。`--ask` のストリーミング描画は実装済み（実 LLM 接続時に表示）。

---

## フェーズ 5: REPL / スラッシュコマンド / サジェスト

**目的**: 対話ループと入力ディスパッチ（`/ @ !` ＋自然言語）、スラッシュコマンド群、補完、割り込みを実装。

**主な成果物**: REPL ループ、`ISlashCommand` レジストリ、`@`コレクタ注入、`!`シェル、サジェスト、キー操作。

**主要型（案）**
```csharp
public interface ISlashCommand { string Name { get; } string Description { get; } string? ArgumentHint { get; }
    ValueTask ExecuteAsync(SlashContext ctx, string args); }
// SlashCommandRegistry（補完候補も提供）
// InputDispatcher: 先頭文字で / @ ! / 自然言語に分岐
// AtContextInjector: @<collector> → スナップショットをプロンプトに構造化注入
// ShellRunner: ! ユーザー起動は直接実行（Actions:AllowShell）、出力取込
```

**参照**: 付録 A（先頭文字分岐・補完 UX・キー操作）、PrettyPrompt。

**チェックリスト**
- [x] REPL ループ（`ReplSession`・`Banner`・`/clear`・`/exit`）
- [x] `InputDispatcher`：`/`→コマンド、`@`→注入、`!`→シェル、他→エージェント
- [x] `ISlashCommand` + レジストリ（DI）、`/help` 自動生成
- [x] 基本コマンド：`/help` `/clear` `/exit` `/info` `/status` `/config` `/model` `/doctor`
- [x] `@<category>` 情報注入（登録コレクタを動的列挙。単独=表示 / 質問付き=エージェントへ注入）
- [x] `!<command>` シェル実行（`cmd /c`・出力取込・`Actions:AllowShell` で制御）
- [x] サジェスト：`PrettyPromptInputReader`（`/`・`@` のポップアップ補完）+ `BuiltinInputReader`（フォールバック/リダイレクト）。**選択は対話端末判定 + `Ui:CompletionEngine`**
- [~] 割り込み：`Ctrl+C`/`Ctrl+D`/EOF → REPL 終了（`IsSuccess=false`/`null`）。生成中断(Esc)・2 回 Ctrl+C・複数行・履歴は PrettyPrompt 既定機能に委譲（実機検証はユーザー）
- [x] CS ファイル UTF-8（BOMなし）+ CRLF

**完了基準**: 自然言語質問・`/info`・`@<cat>`・`!cmd`・コマンド一覧・終了がすべて動作。✅ **達成**（0 警告 / 0 エラー、Builtin 経路をパイプ入力で全確認）。PrettyPrompt の `/`・`@` 補完はコンパイル検証済みで、**対話端末での補完表示はユーザー確認**（C7）。

---

## フェーズ 6: 診断ロジック（外部ルール／閾値エンジン）

**目的**: 外部 JSON の閾値・ルールをロードして決定的に診断し、レポートとダッシュボードを生成する。

**主な成果物**: 閾値/ルールローダー（ホットリロード）、ルールエンジン、`DiagnosisReport`、ダッシュボード描画、`/health`/`/diagnose`/`/report`/`/rules`。

**主要型（案）**
```csharp
// ThresholdSet（rules/thresholds.json）/ RuleDefinition{ Id, Metric, Op, Value(リテラル|{ref}), Severity, Message, Action }
public interface IRuleEngine { DiagnosisReport Evaluate(Snapshot snapshot); }
// MetricPathResolver: "storage.lifeRemainingPercent" → 値
// DiagnosisReport(Timestamp, Severity Overall, IReadOnlyList<Finding>, IReadOnlyList<RecommendedAction>)
// reloadOnChange でホットリロード、/rules reload で明示再読込
```

**参照**: 仕様 §13.1/§13.2（JSON スキーマ）、§5.2（初期ルール表・**仮値**）。

**チェックリスト**
- [ ] `rules/thresholds.json` / `rules/rules.json` の雛形（仮値）を同梱
- [ ] ローダー（`Microsoft.Extensions.Configuration` or 専用 JSON、`reloadOnChange`）
- [ ] `MetricPathResolver`（スナップショット → メトリクスパス）
- [ ] `IRuleEngine`：演算子（`<,<=,>,>=,==,!=`）・`{ref}` 閾値参照・重大度判定
- [ ] `DiagnosisReport` 生成（ルールが直接生成。LLM 非依存）
- [ ] 診断ダッシュボード描画（**枠なしテーブル + 絵文字 ✅/⚠️/❌ + `BarChart`**）
- [ ] `/health`（概況）/ `/diagnose [category]` / `/report [--save]` / `/rules [reload]`
- [ ] `DiagnoseCommand`（非対話・`--json`）が同エンジンを再利用
- [ ] LLM はレポートを受けて説明・優先順位をストリーミング（構造化はルール側）

**完了基準**: 外部ファイルの値変更が再コンパイルなしで反映。`/health`/`/diagnose` がダッシュボード表示。警告ゼロ。

---

## フェーズ 7: RAG / コンテキストプロバイダー

**目的**: しきい値根拠・対処ポリシーを RAG で出典付き注入し、現在スナップショットを毎ターン注入する。

**主な成果物**: `TextSearchProvider`（ナレッジ検索）、`PcContextProvider`（スナップショット注入）、エージェントへの登録。

**主要型（案）**
```csharp
// knowledge/ 配下のポリシー文書を読み込み TextSearchProvider に供給（SourceName=出典）
// PcContextProvider : AIContextProvider → ProvideAIContextAsync で時刻/主要メトリクスを Instructions 注入
// ChatClientAgentOptions { ChatOptions{Instructions,Tools}, AIContextProviders=[rag, snapshot] } へ移行
```

**参照**: Feature10（`TextSearchProvider`/`TextSearchResult`）、Feature06（`AIContextProvider`/`AIContext`）。

**チェックリスト**
- [ ] `knowledge/`（ポリシー/対処）ローダー、`Rag:Enabled`/`Rag:KnowledgePath` 反映
- [ ] `TextSearchProvider` 構築（検索デリゲート、`SourceName` 出典）
- [ ] `PcContextProvider`（スナップショット注入、プロンプトインジェクション対策）
- [ ] エージェント生成を `ChatClientAgentOptions` + `AIContextProviders` へ移行
- [ ] 「根拠が無いことは推測しない」指示（幻覚抑制）
- [ ] 出典が回答に提示されることを確認

**完了基準**: 診断助言にポリシー出典が反映され、毎ターン現在値が注入される。警告ゼロ。

---

## フェーズ 8: HITL アクション

**目的**: 承認を伴う修復アクション（一時ファイル削除、bin/obj 削除）を安全に実装する。

**主な成果物**: `ApprovalRequiredAIFunction` ラップ、承認フロー（中断→TUI 承認→再開）、クリーンアップツール、`/actions`/`/clean`。

**主要型（案）**
```csharp
// MaintenanceTools.CleanTemporaryFiles / CleanBinObj(roots) … 列挙→承認→削除（既定は列挙）
// 承認: ToolApprovalRequestContent 検出 → TUI で「今回のみ/常に/中止」→ request.CreateResponse(approved) を ChatRole.User で再実行
// セッション必須（CreateSessionAsync）。ChatMessage は MEAI 別名に注意
```

**参照**: Feature05（承認フロー・`ApprovalRequiredAIFunction`・`ToolApprovalRequestContent`・`CreateResponse`）、`MaintenanceTools`（安全実装）。

**チェックリスト**
- [ ] セッション対応（`CreateSessionAsync` / `RunAsync(msg, session)`）
- [ ] 一時ファイル列挙→（承認時）削除ツール（`%TEMP%` 等）
- [ ] bin/obj 削除ツール（**探索ルート毎回明示**、`.csproj` 近傍、除外パターン、列挙→承認→削除）
- [ ] `ApprovalRequiredAIFunction` でラップ、`Actions:RequireApproval` 反映
- [ ] TUI 承認 UI（今回のみ/常に/中止）→ `CreateResponse` で再開
- [ ] `/actions`（一覧）/ `/clean temp` / `/clean binobj <root>`
- [ ] LLM 起動シェルは承認必須（ユーザー起動 `!` と区別）

**完了基準**: 承認なしに破壊操作が走らず、承認後に削除実行。警告ゼロ。

---

## フェーズ 9: 可観測性（OTEL）

**目的**: 2 層の処理時間ログと、既定オフの OTLP エクスポートを Agent プロジェクト内に実装する。

**主な成果物**: CLI フィルタ計測 + エージェント/ツールミドルウェア計測、`UseOpenTelemetry`、OTel SDK + OTLP、（任意）Aspire AppHost。

**主要型（案）**
```csharp
// エージェント: AsBuilder().Use(実行5引数 計測).Use(ツール4引数 計測).UseOpenTelemetry(sourceName, t=>t.EnableSensitiveData=cfg).Build();
// OTel: TracerProvider + AddSource(sourceName) + AddOtlpExporter(endpoint)  ※ Telemetry:Otlp:Enabled で分岐
// 任意: MeterProvider + Meter（収集件数/診断重大度/処理時間ヒストグラム）
```

**参照**: Feature04（ミドルウェア 2 層）、Feature08（`UseOpenTelemetry`）。**OTLP/メトリクスはサンプルに無い**ため新規実装。

**チェックリスト**
- [ ] CLI フィルタ（`ExecutionTimeFilter`）で全コマンド時間ログ（P1 と統合）
- [ ] エージェント実行ミドルウェア（5 引数）・ツールミドルウェア（4 引数）で時間ログ
- [ ] `UseOpenTelemetry(sourceName, …)`、`EnableSensitiveData` 既定オフ
- [ ] `OpenTelemetry` / `OpenTelemetry.Exporter.OpenTelemetryProtocol` 追加
- [ ] `TracerProvider` + `AddSource(sourceName)` + `AddOtlpExporter`（**`Telemetry:Otlp:Enabled` 既定オフ**）
- [ ] 送信先 OpenTelemetry Collector / Aspire Dashboard で受信確認
- [ ] （任意）`MeterProvider` + カスタムメトリクス
- [ ] （任意）Aspire AppHost プロジェクト（アプリ本体は非依存・`ServiceDefaults` は作らない）

**完了基準**: 既定では送信せずローカルログのみ。設定有効化で Collector/Dashboard にトレース到達。警告ゼロ。

---

## フェーズ 10: カスタムコマンド + オプション機能 + 仕上げ

**目的**: Markdown カスタムコマンド、終了サマリ、任意のオプション機能、発行・ドキュメントを整える。

**主な成果物**: カスタムコマンドローダー、終了サマリ、（任意）評価/圧縮、publish、README。

**主要型（案）**
```csharp
// CustomCommandLoader: Customization:CommandsPaths（2層・プロジェクト優先）から *.md（frontmatter）を読み ISlashCommand 化
//   $ARGUMENTS/$1, !`cmd`, @path を展開
// ExitSummary: 発見問題数・確認項目・所要時間
```

**参照**: 付録 A（カスタムコマンド形式）、Feature14（評価）/Feature13（圧縮、`MAAI001` 局所抑制は事前相談）。

**チェックリスト**
- [ ] Markdown カスタムコマンド（frontmatter `name/description/argument-hint`、`$ARGUMENTS`/`!cmd`/`@path`）
- [ ] グローバル/プロジェクト 2 層（プロジェクト優先）の探索
- [ ] 終了時サマリ表示
- [ ] （任意）評価（`LocalEvaluator`）を CI 用に
- [ ] （任意）履歴圧縮（`CompactionProvider`、`MAAI001` 抑制は事前相談）
- [ ] publish：SelfContained / SingleFile / **Trimming なし** で単一 exe 生成
- [ ] `README.md` 更新（使い方・前提=管理者権限・設定）
- [ ] 全フェーズ通しの手動シナリオ確認

**完了基準**: カスタムコマンドが追加で動作し、単一 exe 発行成功。警告ゼロ。

---

## リスクと対策

| リスク | 対策 |
| --- | --- |
| Spectre `Live` のビューポート超過（C1） | 本文はスクロールバックへ逐次 Write、一過性 UI のみ Live（§9.3）。P4 PoC で確定 |
| PrettyPrompt × Spectre の協調（C7） | P4 PoC で検証、不可なら Builtin 補完にフォールバック |
| 管理者権限なしでセンサー/SMART 取得不可 | 権限判定し縮退。`/doctor` で可用性提示 |
| 実験的 API（`MAAI001`） | 採用は任意機能のみ・局所抑制は**事前相談** |
| LibreHardwareMonitor とトリミング非互換 | **Trimming なし**で確定（リスク回避済み） |
| LLM 接続未設定 | 起動時に案内表示して縮退（情報取得系は LLM なしでも動く設計） |

---

## 付録: フェーズ × 仕様セクション対応

| フェーズ | 主な仕様参照 |
| --- | --- |
| P0/P1 | §3, §4.1, §11, §12 |
| P2 | §4.4, §5.1 |
| P3 | §6.1, §6.3, §7 |
| P4 | §8.5–8.9, §9 |
| P5 | §8.1–8.4, 付録 A |
| P6 | §5.2, §13.1, §13.2 |
| P7 | §6.1（RAG/Context） |
| P8 | §5.3 |
| P9 | §10 |
| P10 | §13.3, §6.2, §11 |

> 次アクション: 本プランの承認後、**フェーズ 0 から実装着手**。各フェーズ末で警告ゼロビルドを確認しながら進める。
