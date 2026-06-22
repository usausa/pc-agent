# 🖥️ PcAgent 仕様書

> **ステータス**: 実装完了（P0–P10・実機検証済み）／最終更新 2026-06-22
> **対象**: Windows・.NET 10・Microsoft Agent Framework（`Microsoft.Agents.AI` 1.10.0）
> **役割分担**: 使い方・コマンド・設定キーの一覧などの**操作リファレンスは [`README.md`](../README.md)**。本書は**目的・設計思想・アーキテクチャ・要件**を扱う設計仕様書。

---

## 📚 目次

1. [目的と設計思想](#1-目的と設計思想)
2. [スコープ](#2-スコープ)
3. [技術スタック](#3-技術スタック)
4. [アーキテクチャ](#4-アーキテクチャ)
5. [機能設計](#5-機能設計)
6. [Microsoft Agent Framework の採用方針](#6-microsoft-agent-framework-の採用方針)
7. [ストリーミングとイベントモデル](#7-ストリーミングとイベントモデル)
8. [CLI / TUI 設計](#8-cli--tui-設計)
9. [可観測性・テレメトリ](#9-可観測性テレメトリ)
10. [非機能要件](#10-非機能要件)
11. [外部ファイル仕様（ルール・閾値・カスタムコマンド）](#11-外部ファイル仕様ルール閾値カスタムコマンド)
12. [設定モデル](#12-設定モデル)
- [付録 A: 参考にしたエージェント CLI と採用機能](#付録-a-参考にしたエージェント-cli-と採用機能)

---

## 1. 目的と設計思想

### 1.1 目的

- Windows PC の**ハードウェア／システム情報を取得**し、**診断**し、必要に応じて**アクション（推奨提示・承認付きの限定的な修復）**を行う対話型エージェント CLI。
- **診断に限らず単純な情報取得**（「CPU 温度は？」「空き容量は？」）も一級の用途とする。
- Microsoft Agent Framework の各機能と、一般的な AI エージェント CLI の実装パターン（`/コマンド`・入力補完・ストリーミング表示）の検証を兼ねる。
- UI はリッチな TUI（絵文字・進捗グラフ）とし、**ストリーミング表示を基本**とする。

### 1.2 設計思想

- **「関数で書けることは関数で書く」**: 閾値判定・異常検知などの決定的処理は LLM に投げず、**外部ファイル化したルール（純ロジック）**で実装する。LLM の価値は **総合的な解釈・優先順位付け・説明・対話的フォローアップ**に置く。
- **エージェント = LLM + ツール + 状態 + ループ**: 情報取得（ツール）と会話状態（セッション）を備え、ストリーミングで逐次提示する。
- **単一ドメイン＝単一エージェント**: マルチエージェント化は行わない（単一ドメインのため。過剰分割を避ける）。
- **拡張容易性**: 収集源（`ICollector`）と診断ルール（外部 JSON）は、本体の再コンパイル・改修なしに追加・調整できる。

---

## 2. スコープ

### 2.1 対象（In Scope）

| 区分 | 内容 |
| --- | --- |
| プラットフォーム | **Windows のみ**（`net10.0-windows10.0.26100.0`） |
| CLI 基盤 | **Smart.CommandLine.Hosting** による Hosting / DI / Configuration / コマンド |
| 情報取得 | CPU / GPU / メモリ / マザーボード(I/O) / ストレージ / ネットワーク / Wi-Fi / バッテリーのセンサー値、ディスク SMART、OS・プロセス・ドライブ、イベントログ等。**`ICollector` 実装の追加で収集源を拡張可能** |
| 診断 | **外部ファイル化したルール**による閾値判定・異常検知 + ルール／ポリシーの RAG 検索 + LLM による総合診断 |
| アクション | 安全な推奨提示、および**承認（HITL）を伴う限定的な修復**（一時ファイル削除、VS プロジェクトの bin/obj 削除、許可リスト制の LLM シェル） |
| 対話 | 単発質問中心の対話 REPL + `/コマンド` + `@`情報注入 + `!`シェル + 入力補完。非対話の単発コマンド実行（スクリプト用途）も対応 |
| UI | Spectre.Console による TUI（ストリーミング・診断ダッシュボード・リソース可視化） |
| 可観測性 | 処理時間ログ、OpenTelemetry による OTLP エクスポート（トレース + メトリクス・既定オフ） |

### 2.2 対象外（Out of Scope）

- Linux / macOS 対応。
- **マルチエージェント**構成。
- 破壊的・高リスクな自動修復（ドライバ更新、レジストリ改変、サービスの恒久的変更、パーティション操作等）。**提示**または**明示承認 + 低リスク操作**に限定する。
- 常駐サービス化・スケジューリング・リモート集中管理。
- 認証情報・個人情報の外部送信を伴う機能。

---

## 3. 技術スタック

| 区分 | 採用 | 版 | 備考 |
| --- | --- | --- | --- |
| ランタイム | .NET | **10**（`net10.0-windows10.0.26100.0`） | `LangVersion=preview` / `Nullable=enable` / `ImplicitUsings=enable`（`Directory.Build.props` 準拠） |
| CLI 基盤 | **Usa.Smart.CommandLine.Hosting** | 2.8.x | `CommandHost` / DI / Configuration / フィルタ。内部は System.CommandLine |
| エージェント基盤 | Microsoft Agent Framework | **1.10.0** | `Microsoft.Agents.AI` / `.Abstractions` / `.OpenAI` |
| LLM 接続（抽象） | `IChatClient` 抽象 + プロバイダー実装 | — | 既定 **Foundry / Azure OpenAI**（`Azure.AI.OpenAI` 2.1.0）。Ollama / Foundry Local も対応（[§6.3](#63-llm-プロバイダーの抽象化)） |
| TUI 出力 | Spectre.Console | 0.57.x | リッチ装飾・`Progress` / `Status` / `BarChart` 等 |
| 入力・補完 | **PrettyPrompt** | 最新 | `/` のポップアップ補完・複数行入力。`Builtin` 補完へフォールバック可 |
| HW センサー | LibreHardwareMonitorLib | 0.9.6 | CPU/GPU/メモリ/マザーボード/ストレージ/ネットワーク/バッテリー |
| ディスク SMART | HardwareInfo.Disk | 1.12.0 | NVMe / Generic(ATA) SMART |
| Wi-Fi | ManagedNativeWifi | 3.0.2 | Native WiFi API ラッパ（接続/信号/バンド/チャネル） |
| イベントログ | System.Diagnostics.EventLog | 10.0.9 | System / Application の直近のエラー・警告 |
| 可観測性 | OpenTelemetry SDK + OTLP Exporter | 1.16.x | `TracerProvider` / `MeterProvider` / OTLP（gRPC・HTTP） |
| ロギング | Microsoft.Extensions.Logging | 10.x | `LoggerMessage` ソース生成で計測 |
| 解析 | StyleCop / NetAnalyzers / JapaneseComment | — | `AnalysisMode=All` / `WarningsAsErrors=nullable` / **警告ゼロ必須** |

> 参考実装: 情報取得=`Service-PrometheusExporter`、エージェント配線=`WorkAgent`（Feature01〜14 / AgentSampleCore）、TUI=`TuiSpectreConsoleAgentSample`、CLI 基盤=`Smart-Net-CommandLine`、単発コマンド雛形=`tool-bt-tool/BleScan`。

---

## 4. アーキテクチャ

### 4.1 レイヤ構成と依存方向

3 つのコアプロジェクトと 2 つの周辺プロジェクトで構成し、依存は**下方向のみ**（上位 UI が下位に依存し、下位は上位を知らない）。

```
PcAgent.Tui (exe)  ──►  PcAgent.Agent (lib)  ──►  PcAgent.Diagnostics (lib)
  CLI / REPL / 描画         AF 配線・LLM 抽象          収集・ルール（決定的・LLM非依存）
        └───────────────────────────────────────►  （Tui は Diagnostics も直接参照）

PcAgent.Evaluation (exe) ──► PcAgent.Agent      品質評価（LocalEvaluator・CI/開発用）
AppHost (Aspire, exe)     （本体に依存しない）   OTLP 受信ダッシュボード（任意）
```

- **UI 境界 = `AgentEvent`**: エージェント実行の結果は UI 非依存の `AgentEvent` 列として `PcAgent.Agent` から提供される。`PcAgent.Diagnostics` / `PcAgent.Agent` は Spectre.Console やコンソールに一切依存しない（UI は差し替え可能）。
- **決定的処理の分離**: 閾値判定・SMART 解釈・修復の列挙/実行は `PcAgent.Diagnostics`（純ロジック）に閉じ、LLM を介さない。LLM は説明・優先順位付け・対話に専念する。
- **承認の逆方向依存は抽象で解決**: 修復ツールの人手承認は `PcAgent.Agent` の `IToolApprovalHandler` を介し、UI 実装（Tui）を注入する（Agent は UI を直接参照しない）。

### 4.2 プロジェクト / モジュール構成

```
PcAgent.Diagnostics   (classlib)  … 情報取得 + ルール判定（決定的・LLM非依存）
  Hardware/      LibreHardwareMonitor ラッパ（HardwareMonitorSource / UpdateVisitor / Readings / NativeMethods）
  Collectors/    ICollector と実装（HardwareCollectors / SmartCollector / SystemCollector / WifiCollector / EventLogCollector）… 拡張ポイント
  Rules/         外部 JSON のルール/閾値をロードし評価（RuleLoader / RuleEngine / RuleDefinition）
  Actions/       承認対象の修復ロジック（MaintenanceService: 一時ファイル・bin/obj の列挙/削除）
  Models/        スナップショット・診断レポートの型（CollectorModels / DiagnosisModels）
  Platform/      AdminChecker（管理者権限判定）
  SnapshotBuilder ／ DiagnosticsMetrics ／ DiagnosticsTelemetry ／ AddDiagnostics(DI)

PcAgent.Agent         (classlib)  … Agent Framework 配線・LLM 抽象
  ChatClientFactory   設定で選んだプロバイダ → IChatClient（Foundry / Ollama / FoundryLocal）
  Tools/              関数ツール（PcInfoTools / MaintenanceTools / ShellTools / ToolFormatter）
  Rag/                PcContextProvider（スナップショット注入）／ KnowledgeStore（RAG）
  PcAgentConversation エージェント生成・セッション・ストリーミング→AgentEvent 変換・履歴圧縮
  AgentEvent          UI 非依存のイベント列（= UI 境界）
  AgentTelemetry      OpenTelemetry（TracerProvider + MeterProvider / OTLP）
  IToolApprovalHandler 承認 UI の抽象（実装は Tui）／ AddPcAgent(DI)

PcAgent.Tui           (exe)       … CLI / REPL / 描画（Smart.CommandLine ホスト）
  Program.cs           CommandHost 構築・DI 登録・テレメトリ初期化/フラッシュ
  Commands/            RootCommandHandler（引数なし→REPL / --ask→単発）／ InfoCommand ／ DiagnoseCommand
  Filters/             ICommandFilter（ExecutionTimeFilter / CancellationFilter）
  Repl/                ReplSession・InputDispatcher（/ @ ! 振り分け）・入力（PrettyPrompt）・スラッシュコマンド群・カスタムコマンド・ShellRunner・ConsoleApprovalHandler
  Rendering/           AgentEvent→Spectre 描画（ConversationRenderer）・info/diagnose 描画・バナー・終了サマリ・区切り線・JSON 出力

AppHost               (Aspire exe) … 単体 OTLP 受信ダッシュボード（本体非依存・任意）
PcAgent.Evaluation    (exe)        … LocalEvaluator による品質評価（CI/開発用）
```

> 実装は**下層（Diagnostics）→ 上層（Tui）**へ段階的に構築した（P0–P10）。共有モデル（`AgentEvent` 等）は UI 非依存にするため `PcAgent.Agent` 側に置く。

### 4.3 実行形態（2 系統）

| 形態 | 例 | 用途 |
| --- | --- | --- |
| 対話 REPL | `pcagent`（引数なし） | 単発質問の連続 + `/コマンド` + `@`注入 + `!`シェル。**主用途** |
| 非対話 単発 | `pcagent info cpu` / `pcagent diagnose` / `pcagent --ask "CPU温度は？"` | スクリプト/パイプ用途。同じ収集・ルールを再利用 |

`RootCommandHandler` が「引数なし起動 → REPL」「`--ask` 等の単発指定 → 一回答して終了」を切り替える。REPL（対話ループ）はフレームワーク非提供のため自前実装する。

### 4.4 リクエストのライフサイクル（データフロー）

```
[ユーザー入力 / 単発コマンド]
     │  (Smart.CommandLine ホスト + REPL)
     ▼
[InputDispatcher]  / → スラッシュコマンド   @ → 情報注入   ! → シェル   その他 → エージェント
     │
     ▼ (エージェント経路)
[PcAgentConversation] ── RunStreamingAsync ──► [LLM (IChatClient: Foundry/Ollama/…)]
     │  ▲  ツール呼び出し                          │ 応答断片(ストリーム)
     ▼  │                                          ▼
[関数ツール] ──► [PcAgent.Diagnostics]      [AgentEvent へ変換（UI 非依存）]
     │            ├ Collectors（HW/SMART/System）   │
     │            └ Rules（外部 JSON・決定的）        ▼
     └ PcContextProvider / KnowledgeStore が注入   [ConversationRenderer → スクロールバック逐次描画]
```

エージェント経路の手順:

1. `InputDispatcher` が先頭文字で振り分ける（`/` `@` `!` / 自然文）。
2. 自然文は `PcAgentConversation.SendAsync` へ:
   - 永続 `AgentSession` を遅延生成（ターン間で会話メモリを保持）。
   - `PcContextProvider`（時刻・権限・最逼迫ドライブ）と `KnowledgeStore`（RAG）が毎ターン文脈を注入。
   - `RunStreamingAsync` を実行し、ミドルウェア（実行 5 引数 / ツール 4 引数）が処理時間を計測。
   - ストリーム断片を `AgentEvent` に変換して逐次返す。
3. ツール呼び出しは `PcAgent.Diagnostics`（Collectors / Rules）へ。修復系ツールは `ApprovalRequiredAIFunction` で承認要求を発行 → `IToolApprovalHandler`（Tui）で承認 → **同一セッションで再開**。
4. `ConversationRenderer` が `AgentEvent` をスクロールバックへ逐次描画（簡易マークダウン整形）。
5. 履歴が閾値を超えると `CompactionProvider` が古いターンを圧縮し、文脈枠を抑制する。

### 4.5 拡張ポイント

- **収集の拡張**: `ICollector`（`Name` / `CollectAsync(...)`）を実装し DI 登録するだけで、新しい情報源を足せる（例: **Wi-Fi（ManagedNativeWifi）・イベントログ（System.Diagnostics.EventLog）は追加済み**）。ツール / `@`プロバイダー / `info` コマンドは登録済みコレクタを列挙して動的に対応する。
- **ルール・閾値の外部化**: 判定ロジックは**外部 JSON**（[§11](#11-外部ファイル仕様ルール閾値カスタムコマンド)）に定義し、エンジンがロードして評価する。再コンパイル不要で変更・追加でき、実行ごとに再読込する。
- **カスタムコマンドの外部化**: Markdown ベースの診断手順（[§11.3](#113-カスタムコマンドmarkdown)）をユーザーが追加でき、`/コマンド` として一覧に現れる。

### 4.6 DI 合成

- `Program.cs` の `CommandHost` が `AddDiagnostics` → `AddPcAgent` → TUI 登録の順で合成する。
- **`AddDiagnostics`**: 設定バインド、`HardwareMonitorSource`、各 `ICollector`、`SnapshotBuilder`、`RuleEngine` / `RuleLoader`、`MaintenanceService`。
- **`AddPcAgent`**: `ChatClientFactory`、ツール群、`PcContextProvider` / `KnowledgeStore`、`PcAgentConversation`、`AgentTelemetry`。
- **TUI**: 描画系、REPL（`ReplSession` / `InputDispatcher`）、スラッシュコマンド（`ISlashCommand` 実装群）、`IToolApprovalHandler`（`ConsoleApprovalHandler`）。

---

## 5. 機能設計

### 5.1 情報取得（Collectors）

LibreHardwareMonitorLib を `Computer { IsCpuEnabled, IsGpuEnabled, IsMemoryEnabled, IsMotherboardEnabled, IsControllerEnabled, IsNetworkEnabled, IsStorageEnabled, IsBatteryEnabled }` で初期化し、`IVisitor`（`Update()` 走査）+ **節流更新**で読み取る。

| カテゴリ | 取得メトリクス（例） | センサー種別 |
| --- | --- | --- |
| CPU | 負荷率 / クロック / 温度 / 電圧 / 電流 / 電力 | Load, Clock, Temperature, Voltage, Current, Power |
| GPU(NVIDIA/AMD/Intel) | 負荷 / クロック / ファン / 温度 / 電力 / VRAM / PCIe 帯域 | Load, Clock, Fan, Temperature, Power, SmallData, Throughput |
| メモリ | 物理/仮想 使用量・空き・使用率 | Data, Load（+ `GetPhysicallyInstalledSystemMemory`） |
| マザーボード(I/O) | ファン回転数 / 温度 / 電圧 / ファン制御 | Fan, Temperature, Voltage, Control |
| ストレージ | 使用率 / R/W バイト・速度 / 温度 / **SSD 寿命** / 予備領域 / 書込増幅 | Load, Data, Throughput, Temperature, Level, Factor |
| ネットワーク | 送受信バイト / 速度 / 負荷 | Data, Throughput, Load |
| バッテリー | 充電率 / 劣化 / 電圧 / 電流 / 容量 / レート / 残時間 | Voltage, Current, Energy, Power, TimeSpan |
| システム | OS / アーキ / マシン名 / 論理プロセッサ数 / .NET / 稼働時間 / ドライブ / 上位プロセス | BCL（`RuntimeInformation`, `DriveInfo`, `Process`） |
| Wi-Fi | SSID / BSSID / 信号(品質・dBm) / リンク速度(Rx/Tx) / バンド / チャネル / PHY(世代) | ManagedNativeWifi（`GetCurrentConnection` / `EnumerateBssNetworks` / `GetRssi`） |
| イベントログ | 直近24h の Critical/Error/Warning 件数 + (プロバイダ+ID) 別の集計 | System.Diagnostics.EventLog（`EventLogReader` で System / Application） |

ディスク SMART は `DiskInfo.GetInformation()`、`SmartType.Nvme`→`ISmartNvme`（`PercentageUsed`, `PowerOnHours`, `Temperature`, `AvailableSpare`(+閾値), `CriticalWarning`, `MediaErrors`, `UnsafeShutdowns` 等）、`SmartType.Generic`→`ISmartGeneric`（`GetSupportedIds()` / `GetAttribute(id).RawValue`）。

> 値が取れない／無効な場合は **`double.NaN`** を欠損マーカーとして扱う。

### 5.2 診断（ルール + RAG + LLM の 3 段）

1. **ルールベース判定（決定的・外部ファイル）** — 取得スナップショットに対し、[§11.1](#111-診断閾値thresholds) の閾値・[§11.2](#112-診断ルールrules) のルールを適用し `Severity`（Info / Warning / Critical）を判定する。閾値は外部ファイルで調整可能。初期しきい値:

   | 観点 | 条件（初期しきい値・調整可） | 重大度 |
   | --- | --- | --- |
   | CPU/GPU 温度 | > 85℃ 継続 / > 95℃ | Warning / Critical |
   | SSD 寿命 | 残り（= 100 − PercentageUsed）< 10% / < 3% | Warning / Critical |
   | SMART 予備領域 | `AvailableSpare` ≤ `AvailableSpareThreshold` | Critical |
   | SMART 重大警告 | `CriticalWarning` ≠ 0 | Critical |
   | SMART メディアエラー | `MediaErrors` > 0 | Warning |
   | ディスク空き | 空き容量 < 10% / < 5% | Warning / Critical |
   | バッテリー劣化 | 劣化率 > 20% / > 40% | Info / Warning |

2. **RAG（`TextSearchProvider`）** — しきい値の根拠・推奨対処・ポリシー等を**ナレッジ**として検索し、出典（`SourceName`）付きで文脈注入する。幻覚抑制のため「根拠が無いことは推測しない」指示を併用する。

3. **LLM 総合診断（ストリーミング）** — ツールでスナップショット＋ルール結果を取得し、RAG ポリシーと突き合わせ、**要約・優先順位・推奨アクション**を自然言語でストリーミング提示する。

   構造化レポートは **LLM ではなくルールが直接生成**する:
   ```csharp
   record DiagnosisReport(
       DateTimeOffset Timestamp,
       Severity Overall,
       IReadOnlyList<Finding> Findings,        // 部位・指標・実測値・しきい値・重大度・出典ルールID
       IReadOnlyList<RecommendedAction> Actions);
   ```

### 5.3 アクション（HITL 承認）

- **推奨アクションの提示**（常に安全）を基本とする。
- **修復アクションは `ApprovalRequiredAIFunction` でラップ**し、`ToolApprovalRequestContent` を検出 → TUI で承認 → `request.CreateResponse(approved, reason)` を `ChatRole.User` として同一セッションで再実行する。承認 UI は「**今回のみ承認 / 常に承認 / 中止**」を提示する。
- 修復アクション:
  - **一時ファイル／不要キャッシュの列挙 →（承認時）削除**（`%TEMP%` 等。安全側＝列挙してから削除）。
  - **VS プロジェクトの `bin` / `obj` 削除**（**探索ルートは既定値を持たず毎回明示指定**。`.csproj` 近傍のみ対象、除外パターン設定可）。
  - **LLM シェル実行**（`ShellTools`）— `Actions:AllowShell=true` のときのみ登録。`Shell:AllowedCommands` の**許可リスト（先頭語一致）**かつ**承認後**にのみ実行し、パイプ/リダイレクト等の演算子（`& | > ^` 等）は拒否する。
- **対象外**: ドライバ/サービス/レジストリ/電源プラン恒久変更等の高リスク操作。

### 5.4 情報取得モード（非診断の単純取得）

- 診断を介さず、**生の情報をそのまま整形表示**する用途を一級でサポートする。
  - 対話: 「CPU 温度は？」「空きディスクは？」→ ツールで取得しストリーミング回答。
  - スラッシュ / `@`: `/info cpu`、`@disk` 等で**罫線なしの整形表示**。
  - 単発コマンド: `pcagent info <category>`（スクリプト/パイプ用途、JSON 構造化出力可）。

---

## 6. Microsoft Agent Framework の採用方針

検証目的のため広く採用するが、各機能は「なぜこのアプリで要るか」を伴って導入する（過剰適用を避ける）。**現在の採用状況（✅）の一覧は [`README.md`](../README.md) の機能表**を参照。本章は採用理由と不採用の判断を扱う。

### 6.1 コア機能（必要性）

| 機能 | このアプリでの必要性 | 主な API |
| --- | --- | --- |
| 関数ツール | 情報取得・ルール結果の取得 | `AIFunctionFactory.Create` |
| ストリーミング | 応答提示の基本（[§7](#7-ストリーミングとイベントモデル)） | `RunStreamingAsync` / `AgentResponseUpdate` |
| セッション | 対話継続・承認の中断/再開・会話メモリ | `CreateSessionAsync` / `RunAsync(msg, session)` |
| ミドルウェア | エージェント実行/ツールの**処理時間ログ**（2 層） | `AsBuilder().Use(...)` |
| コンテキストプロバイダー | 現在のスナップショット（時刻・権限・主要メトリクス）を毎ターン注入 | `AIContextProvider` / `AIContext` |
| RAG | しきい値・ポリシー・対処ナレッジの検索注入 | `TextSearchProvider` |
| ツール承認(HITL) | 修復アクションの人手承認 | `ApprovalRequiredAIFunction` / `ToolApprovalRequestContent` |
| テレメトリ | トレース + メトリクス + **OTLP エクスポート** | `UseOpenTelemetry` + OTel SDK |

### 6.2 任意機能の判断

| 機能 | 判断 | 理由 |
| --- | --- | --- |
| 評価（ローカル検査・`LocalEvaluator`） | **採用** | CI/開発での回帰防止（`PcAgent.Evaluation`）。追加 LLM 呼び出し不要 |
| 履歴圧縮（`CompactionProvider`） | **採用** | 長時間セッションの文脈枠抑制（決定的）。実験的 `MAAI001` を局所抑制 |
| 構造化出力（`RunAsync<T>`） | 補助のみ | 診断レポートは**ルールが直接生成**するため不要（ストリーミングと相反） |
| TODO / ファイルアクセス Provider | 不採用 | 本ツールの用途では過剰 |
| マルチエージェント（ワークフロー/A2A） | 不採用 | 単一ドメインのため（[§1.2](#12-設計思想)・[§2.2](#22-対象外out-of-scope)） |

> 実験的 API（`MAAI001`）の採用箇所のみ局所 `#pragma` で抑制する（**警告抑制は事前確認**: AGENTS.md）。

**評価の深掘り方針**: 現在の評価（`PcAgent.Evaluation` / `LocalEvaluator`）は**振る舞い検査**（接地＝ツールで実値取得 / 正しいツール選択 / 応答内容の部分一致 / 非空）に留め、回帰防止を目的とする。**「数値が妥当か」「診断が的確か」という内容面の評価が必要になった場合は、LLM ではなく決定的な照合で実施する**:

- **数値の妥当性** … 応答中の値を、ツールが返したスナップショット（実測値）や妥当レンジと自前ロジックで照合する。
- **診断の的確さ** … **決定的なルールエンジンの `Findings` を正解**とし、LLM の指摘・要約がそれと整合するかを照合する（診断は決定的に算出済み＝正解が既知のため、判定に LLM を要しない）。

いずれも設計思想（[§1.2](#12-設計思想)「関数で書けることは関数で書く」）に沿って**局所検査**で実装する。**LLM-as-judge（`Microsoft.Extensions.AI.Evaluation`）は採用しない**。

### 6.3 LLM プロバイダーの抽象化

- エージェントは**設定で選択した `IChatClient`** から `.AsAIAgent(...)` で生成する（接続生成を `ChatClientFactory` に集約）。
- 既定: **Foundry / Azure OpenAI**（`AzureOpenAIClient(Uri, ApiKeyCredential).GetChatClient(deployment)`）。
- **Ollama / Foundry Local**: OpenAI 互換エンドポイント経由で同じ `IChatClient` 抽象に載せる。
- 設定キーは [§12](#12-設定モデル) の `Llm:*`。Foundry 互換の `Foundry__*` も受け付ける。

---

## 7. ストリーミングとイベントモデル

> 本プロジェクトは**ストリーミング処理を基本**とする。

### 7.1 方針

- エージェント実行は `RunAsync`（一括）ではなく **`RunStreamingAsync`** を基本とし、`AgentResponseUpdate` を逐次受け取る（各 `ToString()` がテキスト断片、`Contents` に `FunctionCallContent` 等）。
- ストリームを **UI 非依存の `AgentEvent` 列**へ変換するブリッジを `PcAgentConversation` に設ける（UI と LLM 実装を分離する境界）。

### 7.2 `AgentResponseUpdate` → `AgentEvent` 変換

| ストリーム上の内容 | 変換後イベント |
| --- | --- |
| テキスト断片 | `TextDelta(text)` |
| `FunctionCallContent` 検出 | `ToolCallStarted(name, args)` |
| ツール結果（`FunctionResultContent` 等） | `ToolCallCompleted(name, result)` |
| 使用トークン（`UsageContent`） | トークン数の集計（`/context` 表示） |
| ストリーム終端 | `ResponseCompleted` |

### 7.3 構造化出力との両立

- `RunAsync<T>`（構造化）は性質上ストリーミングと相反する。よって **診断レポート（構造化）はルール（決定的）で直接生成**し、LLM はストリーミングで説明・推奨を提供する設計とする。

### 7.4 CJK ストリーミングの分割

- 日本語の桁ずれ回避のため、**CJK は 1 文字単位**、英数は単語単位、改行は独立トークンとして整形してから描画する。

---

## 8. CLI / TUI 設計

### 8.1 入力モデル（先頭 1 文字でモード分岐）

| 先頭 | モード | 例 | 補完 |
| --- | --- | --- | --- |
| `/` | スラッシュコマンド | `/diagnose` `/info cpu` `/help` | コマンド一覧をポップアップ・ファジー絞り込み・説明インライン表示 |
| `@` | 情報注入 | `@cpu` `@disk` `@smart` | 登録コレクタ名を補完。スナップショットをプロンプトに構造化注入 |
| `!` | シェル実行 | `!systeminfo` | ユーザー起動は直接実行し出力取込（`Actions:AllowShell` に従う）。LLM 起動のシェルは承認必須 |
| それ以外 | 自然言語質問 | `CPU温度は？` | エージェントへ（ストリーミング回答） |

> スラッシュコマンドの一覧と機能は [`README.md`](../README.md) を参照。ユーザー定義の `/コマンド` を Markdown で追加できる（[§11.3](#113-カスタムコマンドmarkdown)）。

### 8.2 表示方式の決定（スクロールバック逐次出力）

応答本文は長文化し得るため、Spectre の `Live` は使わず **確定した本文を通常のスクロールバックへ逐次出力**する方式に確定した。これにより以下を**原理的に回避**する:

- ビューポート超過（`Live` 対象が端末高を超えると描画破綻・ジャンプ・切れ）。
- 長文ストリームでの再描画コスト・ちらつき。
- リダイレクト/ヘッドレス時の破綻（全イベントを逐次 `Write` するためパイプでも安全）。

一過性 UI（思考スピナー・収集進捗・ツール状態）のみ、**高さ限定**で `Status` / `Progress` を `Console.IsOutputRedirected` ガード付きに用いる。構造化表示（`info` / 診断ダッシュボード）は `AnsiConsole.Write` / `MarkupLine` による静的描画とする。ストリーム本文は `Markup.Escape` で都度エスケープし、途中整形は行わない（部分マークアップ破綻の回避）。

### 8.3 装飾・CJK 方針

- **罫線（テーブル/パネルの枠線）は使用しない**。代わりに `ラベル: 値` 行 + 幅 1 のブロックバー（`█░`）で構成し、CJK 端末の枠ずれ（曖昧幅）を原理回避する。区切り線が必要な箇所は桁揃えに影響しない `─` を用いる。
- **絵文字・進捗グラフ（`BarChart` / `Progress`）は使用する**。絵文字は非整列のヘッダ位置のみ。
- 配色（暗背景前提）: 応答本文=白、コード=`#b1b9f9` 系、見出し/役割=blue/aqua/green、強調/箇条書き=暖色（赤/黄）、思考・状態=yellow。`grey`(#808080) は不使用。入力・外部値には `Markup.Escape` を必ず適用する。

### 8.4 割り込み・複数行・リダイレクト

- **`Esc`=生成中断（セッション維持）**、**`Ctrl+C` 2 回=終了**、`Ctrl+D`=EOF 終了。`↑↓` / `Ctrl+R` で履歴、複数行は `Ctrl+J`。生成中断は `RunStreamingAsync` の `CancellationToken` で実現する。
- 入力リダイレクト時は補完/プロンプトを避け `Console.ReadLine`、出力リダイレクト時は一過性 UI を抑止する。

---

## 9. 可観測性・テレメトリ

- **2 層の処理時間ログ**: **CLI レベル**＝Smart.CommandLine の `ICommandFilter`（コマンド全体の時間・例外）、**エージェントレベル**＝Agent Framework ミドルウェア（実行全体 5 引数 / 各ツール 4 引数）。
- 計装は `AsBuilder().UseOpenTelemetry(sourceName, ...)`。トレース源は `PcAgent.Diagnostics`（`diagnostics.snapshot`）と `PcAgent.Agent`（エージェント/ツール）。
- **メトリクス**（`DiagnosticsMetrics`）: `pcagent.diagnoses.count` / `pcagent.findings.count`（重大度別）/ `pcagent.collections.count`（Counter）/ `pcagent.snapshot.duration`（Histogram）。
- **OTLP エクスポート**: `TracerProvider` + `MeterProvider` を構築し、`AddOtlpExporter`（gRPC=4317 / HTTP=4318）で送信する。**既定オフ**（`Telemetry:Otlp:Enabled=false`）、設定で有効化。標準の `OTEL_EXPORTER_OTLP_ENDPOINT` があればそれを優先（Aspire 連携）。
- 短命 CLI のため、テレメトリは**起動時に先行初期化**し、終了時に**明示フラッシュ**する（バッチ送信が出る前にプロセスが終わらないように）。
- **OTel 構成は `PcAgent.Agent` 内に実装**する（`ServiceDefaults` は作らない）。受信確認用の **Aspire `AppHost` は本体に依存しない独立プロジェクト**（任意）。
- `EnableSensitiveData` は**既定オフ**（個人 PC 情報・プロンプト/応答の混入注意）。

---

## 10. 非機能要件

| 区分 | 要件 |
| --- | --- |
| ビルド品質 | **警告ゼロ**（`AnalysisMode=All` / `WarningsAsErrors=nullable`）。警告抑制が必要な場合は**事前相談**（AGENTS.md）。 |
| コーディング規約 | `.editorconfig` 準拠。**メンバ変数に `_` プレフィックスを付けない**。数値整形は `CultureInfo.InvariantCulture` + `String.Create`（CA1305/1304）。 |
| 文字コード | ソース（`.cs`/`.csproj`/`.json`/`.md`）は **UTF-8 BOMなし + CRLF**。コンソールは UTF-8。 |
| 実験的 API | `MAAI001` 使用箇所のみ局所 `#pragma` 抑制。 |
| 権限 | センサー/SMART 取得は**管理者権限が必要**。非管理者時は明示メッセージで縮退（取得可能な範囲のみ）。`/doctor` で権限を確認。 |
| スレッド安全性 | センサー更新と読取の競合に注意（`Lock` + 節流）。 |
| 性能 | 収集は節流（既定 1 秒程度）。SMART は別間隔（既定 10 秒）。 |
| 機密情報 | API キーは user-secrets / 環境変数（`Llm__*` / `Foundry__*`）。コミット禁止。トレースへの PII 混入注意。 |
| 配布 | 単一ファイル発行（SelfContained / SingleFile）。**Trimming は行わない**（`PublishTrimmed=false`。LibreHardwareMonitor 等の互換性のため）。 |

---

## 11. 外部ファイル仕様（ルール・閾値・カスタムコマンド）

「ルールは完全な外部ファイル化により変更可能に」「診断閾値も外部ファイル化」の方針を満たす。形式は **JSON**（`Microsoft.Extensions.Configuration` と親和）。

### 11.1 診断閾値（thresholds）

```jsonc
// rules/thresholds.json
{
  "cpu": { "tempWarn": 85, "tempCrit": 95 },
  "gpu": { "tempWarn": 85, "tempCrit": 95 },
  "ssd": { "lifeRemainWarn": 10, "lifeRemainCrit": 3 },
  "disk": { "freePercentWarn": 10, "freePercentCrit": 5 },
  "battery": { "degradationInfo": 20, "degradationWarn": 40 }
}
```

### 11.2 診断ルール（rules）

ルール = 「対象メトリクス・条件（演算子＋閾値参照）・重大度・メッセージ・推奨アクション」。比較ベース。

```jsonc
// rules/rules.json
[
  {
    "id": "ssd-life-low",
    "metric": "storage.lifeRemainingPercent",
    "op": "<",
    "value": { "ref": "ssd.lifeRemainWarn" },   // 閾値ファイル参照
    "severity": "Warning",
    "message": "SSD の寿命残量が少なくなっています（{value}%）。",
    "action": "backup-and-plan-replacement"
  },
  {
    "id": "smart-critical-warning",
    "metric": "smart.criticalWarning",
    "op": "!=", "value": 0,
    "severity": "Critical",
    "message": "SMART 重大警告が立っています。早急なバックアップを推奨します。",
    "action": "backup-now"
  }
]
```

- エンジンはスナップショットの**メトリクスパス**（`storage.lifeRemainingPercent` 等）を解決し、演算子で評価する。実行ごとに再読込する。

### 11.3 カスタムコマンド（Markdown）

**Markdown + frontmatter** 形式。`Customization:CommandsPaths`（グローバル/プロジェクトの 2 層、プロジェクト優先）に配置する。

```markdown
---
name: network-check
description: ネットワーク周りをまとめて確認する
argument-hint: [interface]
---
@network
以下を実行し、結果を踏まえて問題があれば指摘してください。
- 速度・負荷の確認
- !`ipconfig /all`
```

- `@<collector>` で情報注入、`` !`cmd` `` でシェル出力注入（`Actions:AllowShell` に従う）、`$ARGUMENTS` / `$1` で引数展開。

---

## 12. 設定モデル

- 解決順は **`appsettings.json` → user-secrets → 環境変数（区切り `__`）**（Smart.CommandLine の `UseDefaults` が読込）。
- **外部化の意図**: 診断ルール/閾値（[§11](#11-外部ファイル仕様ルール閾値カスタムコマンド)）とカスタムコマンドは設定パスから読み、再コンパイルなしに変更できる。
- **秘匿情報**: API キーは user-secrets / 環境変数（`Llm__*` / `Foundry__*`）。コミットしない。
- **OTLP は既定オフ**。テレメトリで PII を送らないよう `EnableSensitiveData` も既定オフ。
- **全設定キー・既定値の一覧は [`README.md`](../README.md) の設定表**を参照（重複を避けるため本書では割愛）。

---

## 付録 A: 参考にしたエージェント CLI と採用機能

著名 OSS エージェント CLI（Claude Code / Gemini CLI / Codex CLI / Aider / Goose / OpenCode / Crush / Cline / Continue / Qwen Code / Cursor / Amazon Q）を横断調査し、本エージェントに採り入れた機能。

| 採用機能 | 主な出典 |
| --- | --- |
| 先頭 1 文字モード分岐（`/` `@` `!`） | 全 CLI 共通 |
| `/` ポップアップ補完 + ファジー + 説明/引数ヒント | Claude Code, Gemini CLI |
| 診断特化コマンド（`/doctor` `/diagnose` `/health` `/report`） | Claude `/doctor`, Codex `/debug-config`, Goose diagnostics |
| リソース可視化（使用率バー/ゲージ/色） | Amazon Q `/usage`, Claude `/context`, Goose ドットゲージ |
| `Esc`=中断 / `Ctrl+C` 2 回=終了 / `Ctrl+J` 複数行 / ストリーミング | 全 CLI 共通 |
| 読取専用 ⇄ 修復のモード分離 + 承認プロンプト（今回のみ/常に/中止） | Claude, Gemini, Codex |
| `@` 情報プロバイダー（`@cpu` `@disk` `@smart` …） | Cline `@problems` 他 |
| Markdown カスタムコマンド（2 層・frontmatter・`$ARGUMENTS`・`` !`cmd` ``・`@path`） | Claude, Gemini, OpenCode 他 |
| 終了時サマリ（発見問題数・確認項目・所要時間） | Gemini CLI, Qwen |
| `/compact`・自動コンパクション（長時間セッション） | Goose, Cline, Amazon Q |

> 実装ライブラリ: 補完＝**PrettyPrompt**（C#）、出力＝**Spectre.Console**。
