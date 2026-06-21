# PC 診断・情報取得エージェント 仕様書（ドラフト）

> ステータス: **Draft v0.3**（レビュー反映済み・実装プラン着手可）
> 作成日: 2026-06-20
> 基となる構想: [`docs/__idea.md`](__idea.md)
> 本書は実装前の合意形成用ドラフトです。確定事項は [§15](#15-要確認事項) に集約しています（v0.3 で全項目確定）。

### v0.3 での主な変更点（レビュー反映）

- 外部ファイル形式=**JSON** に確定。**`!` シェル実行を対応**に追加。
- **PrettyPrompt 採用**（最終可否は §9 PoC）。配布は **Trimming なし**。
- OTLP 送信先=**OpenTelemetry Collector / Aspire Dashboard**。**OTel 構成は Agent プロジェクト内**に実装（`ServiceDefaults` は作らない。Aspire AppHost は任意）。
- bin/obj 削除の**探索ルートは毎回明示**。閾値は**仮値で着手**し外部ファイルで調整。
- [§15](#15-要確認事項) の残論点はすべて解決。実装プラン着手可。

### v0.2 での主な変更点（レビュー反映）

- LLM プロバイダーを**抽象化**（当面 Microsoft Foundry、Ollama / Foundry Local も視野）。
- **マルチエージェントは対象外**に変更。
- アクションに **VS プロジェクトの bin/obj 削除**を追加。
- **診断だけでなく単純な情報取得**も対象に明記。
- コンソール基盤を **Smart.CommandLine.Hosting**（Hosting/DI）に決定。単発コマンドは **BleScan** を雛形にする。
- 対話は**単発質問中心 + `/コマンド`メニュー + 入力サジェスト**（既存 AI エージェント CLI を参考）。
- **ルール・診断閾値を外部ファイル化**（再コンパイル不要で変更可能）。アーキテクチャは **WiFi 等の収集追加**を想定した拡張性を持たせる。
- **OTLP 送信は既定オフ**・設定で有効化。装飾は**日本語優先・罫線不使用・絵文字/進捗グラフ使用**で確定。
- **設定一覧**（[§12](#12-設定一覧)）と**参考エージェント CLI の採用機能**（[付録 A](#付録-a-参考にしたエージェント-cli-と採用機能)）を追加。

---

## 目次

1. [目的と背景](#1-目的と背景)
2. [スコープ](#2-スコープ)
3. [技術スタック](#3-技術スタック)
4. [全体アーキテクチャ](#4-全体アーキテクチャ)
5. [機能要件](#5-機能要件)
6. [Microsoft Agent Framework 機能の採用方針](#6-microsoft-agent-framework-機能の採用方針)
7. [ストリーミング処理方針](#7-ストリーミング処理方針)
8. [CLI / TUI 設計](#8-cli--tui-設計)
9. [Spectre.Console + ストリーミングの検証計画](#9-spectreconsole--ストリーミングの検証計画)
10. [可観測性・テレメトリ（OTEL）](#10-可観測性テレメトリotel)
11. [非機能要件](#11-非機能要件)
12. [設定一覧](#12-設定一覧)
13. [外部ファイル仕様（ルール・閾値・カスタムコマンド）](#13-外部ファイル仕様ルール閾値カスタムコマンド)
14. [段階的実装方針](#14-段階的実装方針)
15. [要確認事項](#15-要確認事項)
- [付録 A: 参考にしたエージェント CLI と採用機能](#付録-a-参考にしたエージェント-cli-と採用機能)

---

## 1. 目的と背景

- Windows PC の**ハードウェア／システム情報を取得**し、それを**診断**して、必要に応じて**アクション（推奨提示・一部の修復）**を行う対話型エージェントを作る。
- **診断に限らず、単純な情報取得**（「CPU 温度は？」「空き容量を見せて」等）も一級の用途とする。
- **Microsoft Agent Framework 1.0 の各種機能の検証**、および**一般的な AI エージェント CLI 実装の実験**を兼ねる。`/コマンド`・入力サジェスト・ストリーミング表示など、最近のエージェント CLI の体験要素も取り入れる。
- UI は **リッチな TUI**（絵文字・進捗グラフ等）とし、**ストリーミング表示を基本**とする。

### 設計上の基本姿勢（参照: WHY-AGENTS.md）

- **「関数で書けることは関数で書く」**。閾値判定・異常検知などの決定的処理は LLM に投げず、**外部ファイル化したルール**（純ロジック）で実装する。LLM の価値は **総合的な解釈・優先順位付け・説明・対話的フォローアップ**に置く。
- エージェントの本質は **LLM + ツール + 状態 + ループ**。情報取得（ツール）と状態（セッション）を備え、ストリーミングで逐次提示する。
- **マルチエージェント化は行わない**（単一ドメインのため。WHY-AGENTS の「過剰分割を避ける」に従う）。

---

## 2. スコープ

### 2.1 対象（In Scope）

| 区分 | 内容 |
| --- | --- |
| プラットフォーム | **Windows のみ**（`net10.0-windows10.0.26100.0`） |
| CLI 基盤 | **Smart.CommandLine.Hosting** による Hosting / DI / Configuration / コマンド |
| 情報取得 | CPU / GPU / メモリ / マザーボード(I/O) / ストレージ / ネットワーク / バッテリーのセンサー値、ディスク SMART、OS・プロセス・ドライブ等。**将来 WiFi 等の収集を追加可能な拡張設計** |
| 診断 | **外部ファイル化したルール**による閾値判定・異常検知 + ルール／ポリシーの RAG 検索 + LLM による総合診断 |
| アクション | 安全な推奨提示、および**承認（HITL）を伴う限定的な修復**（一時ファイル削除、**VS プロジェクトの bin/obj 削除**等） |
| 対話 | 単発質問中心の対話 REPL + `/コマンド`メニュー + 入力サジェスト。非対話の単発コマンド実行（スクリプト用途）も対応 |
| UI | Spectre.Console による TUI（ストリーミング・診断ダッシュボード・リソース可視化） |
| 可観測性 | 処理時間ログ、OpenTelemetry による OTLP エクスポート（既定オフ） |

### 2.2 対象外（Out of Scope）※ v1 時点

- Linux / macOS 対応。
- **マルチエージェント**構成。
- 破壊的・高リスクな自動修復（ドライバ更新、レジストリ改変、サービスの恒久的変更、パーティション操作等）。v1 は**提示**または**明示承認 + 低リスク操作**に限定。
- 常駐サービス化・スケジューリング・リモート集中管理（PrometheusExporter の領域）。
- 認証情報・個人情報の外部送信を伴う機能。

---

## 3. 技術スタック

| 区分 | 採用 | 版（目安） | 備考 |
| --- | --- | --- | --- |
| ランタイム | .NET | **10**（`net10.0-windows10.0.26100.0`） | `LangVersion=preview` / `Nullable=enable` / `ImplicitUsings=enable`（既存 `Directory.Build.props` 準拠） |
| CLI 基盤 | **Usa.Smart.CommandLine.Hosting** | 2.8.x | `CommandHost` / DI / Configuration / フィルタ。内部は System.CommandLine |
| 引数解析 | System.CommandLine | 2.0.x | 上記が利用 |
| エージェント基盤 | Microsoft Agent Framework | **1.0 GA（参照 1.10.0）** | `Microsoft.Agents.AI` / `.Abstractions` / `.OpenAI` |
| LLM 接続（抽象） | `IChatClient` 抽象 + プロバイダー実装 | — | **当面 Foundry/Azure OpenAI**（`Azure.AI.OpenAI` 2.1.0）。Ollama / Foundry Local も視野（[§6.3](#63-llm-プロバイダーの抽象化)） |
| TUI 出力 | Spectre.Console | **0.57.0** | リッチ装飾・`Live`/`Status`/`Progress`/`BarChart` 等 |
| 入力・補完 | **PrettyPrompt**（採用） | 最新 | `/` のポップアップ補完・複数行入力。最終可否は [§9](#9-spectreconsole--ストリーミングの検証計画) の PoC で確認 |
| HW センサー | LibreHardwareMonitorLib | **0.9.6** | CPU/GPU/メモリ/マザーボード/ストレージ/ネットワーク/バッテリー |
| ディスク SMART | HardwareInfo.Disk | **1.12.0** | NVMe / Generic(ATA) SMART |
| 可観測性 | OpenTelemetry SDK + OTLP Exporter | （追加）| **サンプルに雛形なし**。本プロジェクトで追加（[§10](#10-可観測性テレメトリotel)） |
| ロギング | Microsoft.Extensions.Logging | 10.x | フレームワーク標準 |
| 解析 | StyleCop / NetAnalyzers / JapaneseComment | 既存 | `AnalysisMode=All` / `WarningsAsErrors=nullable` / **警告ゼロ必須** |

> 参考実装: 情報取得=`Service-PrometheusExporter`、エージェント配線=`WorkAgent`（Feature01〜14 / AgentSampleCore）、TUI=`TuiSpectreConsoleAgentSample`、CLI 基盤=`Smart-Net-CommandLine`、単発コマンド雛形=`tool-bt-tool/BleScan`。

---

## 4. 全体アーキテクチャ

### 4.1 CLI 基盤（Smart.CommandLine.Hosting）

- エントリは `CommandHost.CreateBuilder(args).UseDefaults()`。`builder.Services`（DI）・`builder.Configuration`（appsettings/env/user-secrets）・`builder.Environment` を ASP.NET Core 風に扱える。
- コマンドは `[Command]` + `ICommandHandler.ExecuteAsync(CommandContext)`、オプションは `[Option<T>]`。**サブコマンドなしの単発**は BleScan 同様 `ConfigureRootCommand(root => root.UseHandler<RootCommandHandler>())`。
- **CLI レベルの横断処理**は `ICommandFilter`（グローバルフィルタ）で実装（実行時間計測・例外ハンドリング）。これは Develop サンプルの `ExecutionTimeFilter` / `ExceptionHandlingFilter` を踏襲。
- **REPL（対話ループ）はフレームワーク非提供**のため自前実装する。ルートコマンドが「引数なし起動 → 対話 REPL」「`--ask` 等の単発指定 → 一回答して終了」を切り替える。

実行形態（2 系統）:

| 形態 | 例 | 用途 |
| --- | --- | --- |
| 対話 REPL | `pcagent`（引数なし） | 単発質問の連続 + `/コマンド` + サジェスト。**主用途** |
| 非対話 単発 | `pcagent diagnose --json` / `pcagent info cpu` / `pcagent --ask "CPU温度は？"` | スクリプト/パイプ用途。同じ収集・ルールを再利用 |

### 4.2 プロジェクト構成（案）

```
PcAgent.slnx
├─ PcAgent.Diagnostics   (classlib, net10.0-windows…)  … 情報取得 + ルール判定（決定的・LLM非依存）
│     ├─ Collectors/     … 収集の拡張ポイント（ICollector 実装を DI 登録）
│     │     ├─ HardwareCollector   (LibreHardwareMonitor)
│     │     ├─ SmartCollector      (HardwareInfo.Disk)
│     │     ├─ SystemCollector     (OS/プロセス/ドライブ, BCL)
│     │     └─ （将来）WifiCollector など … 追加は実装+登録のみ
│     ├─ Rules/          … 外部ファイルのルール/閾値をロードし評価するエンジン
│     └─ Models/         … スナップショット / 診断レポートの型
│
├─ PcAgent.Agent         (classlib, net10.0-windows…)  … Agent Framework 配線
│     ├─ ChatClients/    … IChatClient プロバイダー（Foundry / Ollama / FoundryLocal）の生成
│     ├─ Tools/          … Collectors/Rules をラップした関数ツール（[Description] 付与）
│     ├─ Providers/      … ContextProvider（スナップショット注入）/ RAG（TextSearchProvider）
│     ├─ Middleware/     … エージェント実行/ツールの処理時間ログ
│     ├─ Telemetry/      … OpenTelemetry 構築（OTLP エクスポート）
│     └─ Streaming/      … RunStreamingAsync → AgentEvent への変換ブリッジ
│
└─ PcAgent.Tui           (exe,     net10.0-windows…)   … CLI/TUI（Smart.CommandLine ホスト）
      ├─ Commands/       … RootCommand（REPL）/ diagnose / info 等の単発コマンド
      ├─ Repl/           … 入力（PrettyPrompt）/ スラッシュコマンド / サジェスト
      ├─ Rendering/      … AgentEvent → Spectre 描画（ストリーミング/ダッシュボード）
      └─ Program.cs      … CommandHost 構築・DI 登録
```

> 共有モデル（`AgentEvent` 等）は TUI 非依存にするため `PcAgent.Agent` 側（必要なら `PcAgent.Core` を新設）。初期は上記 3 プロジェクトに収める。

### 4.3 データフロー

```
[ユーザー入力 / 単発コマンド]
     │  (Smart.CommandLine ホスト + REPL)
     ▼
[入力ディスパッチ]  / → スラッシュコマンド   @ → 情報注入   ! → シェル   それ以外 → エージェント
     │
     ▼ (エージェント)
[PcAgent.Agent] ── RunStreamingAsync ──► [LLM (IChatClient: Foundry/Ollama/…)]
     │  ▲                                      │ 応答断片(ストリーム)
     ▼  │ ツール呼び出し                        ▼
[関数ツール] ──► [PcAgent.Diagnostics]    [Streaming ブリッジ: AgentEvent へ変換]
     │              ├ Collectors（HW/SMART/System/…）   │
     │              └ Rules（外部ファイル・決定的）       ▼
     └─ RAG/ContextProvider が注入        [Spectre 描画（枠なし・絵文字・BarChart）]
```

### 4.4 拡張性（収集・ルールの追加を容易に）

- **収集の拡張**: `ICollector`（例: `string Name`, `Task<CollectorResult> CollectAsync(...)`）を実装し DI に登録するだけで、新しい情報源（**WiFi**、温度センサー追加、イベントログ等）を足せる。ツール／`@`プロバイダー／`info` コマンドは登録済みコレクタを列挙して動的に対応する。
- **ルール・閾値の外部化**: 判定ロジックは**外部ファイル**（[§13](#13-外部ファイル仕様ルール閾値カスタムコマンド)）に定義し、エンジンがロードして評価する。再コンパイル不要で変更・追加でき、`/rules reload` でホットリロード。
- **カスタムコマンドの外部化**: Markdown ベースの診断手順（[§13.3](#133-カスタムコマンドmarkdown)）をユーザーが追加できる。

---

## 5. 機能要件

### 5.1 情報取得（PcAgent.Diagnostics / Collectors）

LibreHardwareMonitorLib を `Computer { IsCpuEnabled, IsGpuEnabled, IsMemoryEnabled, IsMotherboardEnabled, IsControllerEnabled, IsNetworkEnabled, IsStorageEnabled, IsBatteryEnabled }` で初期化し、`IVisitor`（`Update()` 走査）+ **節流更新**で読み取る（PrometheusExporter の `HardwareMonitorInstrumentation` / `UpdateVisitor` を踏襲）。

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
| **（将来）WiFi** | SSID / 信号強度 / リンク速度 / バンド / チャネル | Native WiFi API（wlanapi）または `netsh wlan` 解析 |

ディスク SMART は `DiskInfo.GetInformation()`、`SmartType.Nvme`→`ISmartNvme`（`PercentageUsed`, `PowerOnHours`, `Temperature`/`TemperatureSensors`, `AvailableSpare`(+閾値), `CriticalWarning`, `MediaErrors`, `UnsafeShutdowns` 等）、`SmartType.Generic`→`ISmartGeneric`（`GetSupportedIds()` / `GetAttribute(id).RawValue`）。

> 値が取れない／無効な場合は **`double.NaN`** を欠損マーカーとして扱う。

### 5.2 診断（外部ルール + RAG + LLM）

3 段構成:

1. **ルールベース判定（決定的・外部ファイル）** — 取得スナップショットに対し、[§13.1](#131-診断閾値thresholds) の閾値・[§13.2](#132-診断ルールrules) のルールを適用し `Severity`（Info / Warning / Critical）を判定。初期ルール案（**閾値は外部ファイルで調整可能**）:

   | 観点 | 条件（初期しきい値・要調整） | 重大度 |
   | --- | --- | --- |
   | CPU/GPU 温度 | > 85℃ 継続 / > 95℃ | Warning / Critical |
   | SSD 寿命 | 残り（= 100 − PercentageUsed）< 10% / < 3% | Warning / Critical |
   | SMART 予備領域 | `AvailableSpare` ≤ `AvailableSpareThreshold` | Critical |
   | SMART 重大警告 | `CriticalWarning` ≠ 0 | Critical |
   | SMART メディアエラー | `MediaErrors` > 0 | Warning |
   | ディスク空き | 空き容量 < 10% / < 5% | Warning / Critical |
   | メモリ使用率 | 高負荷が継続 | Info / Warning |
   | バッテリー劣化 | 劣化率 > 20% / > 40% | Info / Warning |

2. **RAG（`TextSearchProvider`）** — しきい値の根拠・推奨対処・ポリシー等を**ナレッジ**として検索し、出典（`SourceName`）付きで文脈注入。幻覚抑制のため「根拠が無いことは推測しない」指示を併用。

3. **LLM 総合診断（ストリーミング）** — ツールでスナップショット＋ルール結果を取得し、RAG ポリシーと突き合わせ、**要約・優先順位・推奨アクション**を自然言語でストリーミング提示。

   構造化レポート型（案。**LLM ではなくルールが直接生成**）:
   ```csharp
   record DiagnosisReport(
       DateTimeOffset Timestamp,
       Severity Overall,
       IReadOnlyList<Finding> Findings,        // 部位・指標・実測値・しきい値・重大度・出典ルールID
       IReadOnlyList<RecommendedAction> Actions);
   ```

### 5.3 アクション（HITL 承認）

- **推奨アクションの提示**（常に安全）を基本とする。
- **修復アクションは `ApprovalRequiredAIFunction` でラップ**し、`ToolApprovalRequestContent` を検出 → TUI で承認 → `request.CreateResponse(approved, reason)` を `ChatRole.User` として同一セッションで再実行（Feature05 踏襲）。承認 UI は「**今回のみ承認 / 常に承認 / 中止**」を提示。
- v1 の修復アクション:
  - **一時ファイル／不要キャッシュの列挙→（承認時）削除**（`%TEMP%`、Windows Update キャッシュ等。安全側＝列挙してから削除）。
  - **VS プロジェクトの `bin` / `obj` 削除**（**探索ルートは既定値を持たず毎回明示指定**。配下を再帰探索し、列挙→承認→削除。`.csproj` 近傍のみ対象、除外パターン設定可）。
  - ごみ箱・スタンバイメモリ等の**サイズ提示**（操作は要承認）。
- **対象外**（v1）: ドライバ/サービス/レジストリ/電源プラン恒久変更等の高リスク操作。

### 5.4 情報取得モード（非診断の単純取得）

- 診断を介さず、**生の情報をそのまま整形表示**する用途を一級でサポートする。
  - 対話: 「CPU 温度は？」「空きディスクは？」→ ツールで取得しストリーミング回答。
  - スラッシュ: `/info cpu`、`/info disk`、`/info smart` 等で**枠なしテーブル＋絵文字**表示。
  - 単発コマンド: `pcagent info <category> [--json]`（スクリプト/パイプ用途、構造化出力可）。

---

## 6. Microsoft Agent Framework 機能の採用方針

検証目的のため広く採用するが、各機能は「なぜこのアプリで要るか」を伴って導入する（過剰適用を避ける）。

### 6.1 コア機能（v1 で必須採用）

| 機能 | このアプリでの用途 | 主な API（参考 Feature） |
| --- | --- | --- |
| 関数ツール | 情報取得・ルール結果の取得 | `AIFunctionFactory.Create`（F01） |
| **ストリーミング** | **応答提示の基本**（[§7](#7-ストリーミング処理方針)） | `RunStreamingAsync` / `AgentResponseUpdate`（F09） |
| 構造化出力 | 診断レポートの型付き取得（補助） | `RunAsync<T>` + STJ ソース生成（F02） |
| セッション | 対話継続・承認の再開 | `CreateSessionAsync` / `RunAsync(msg, session)`（F03） |
| ミドルウェア | エージェント実行/ツールの**処理時間ログ**（2 層） | `AsBuilder().Use(...).Build()`（F04） |
| コンテキストプロバイダー | 現在のスナップショット（時刻・主要メトリクス）を毎ターン注入 | `AIContextProvider` / `AIContext`（F06） |
| RAG | しきい値・ポリシー・対処ナレッジの検索注入 | `TextSearchProvider`（F10） |
| ツール承認(HITL) | 修復アクションの人手承認 | `ApprovalRequiredAIFunction` / `ToolApprovalRequestContent`（F05） |
| テレメトリ | トレース + **OTLP エクスポート** | `UseOpenTelemetry` + OTel SDK（F08 + 追加） |

### 6.2 オプション機能（検証ショーケースとして段階導入）

| 機能 | 用途案 | 区分 | メモ |
| --- | --- | --- | --- |
| 評価（ローカル検査） | CI で応答の妥当性を自動採点 | 🟢 | `LocalEvaluator`（F14） |
| 履歴圧縮 | 長時間セッションの文脈枠抑制（決定的） | 🧪 | `CompactionProvider`（F13、`MAAI001`） |
| TODO 管理 | 多段診断の計画/進捗管理 | 🧪 | `TodoProvider`（F11、`MAAI001`） |
| ファイルアクセス | 診断レポートの保存/読返し | 🧪 | `FileAccessProvider`（F12、`MAAI001`） |

> **マルチエージェント（F07）は採用しない**（[§2.2](#22-対象外out-of-scope-v1-時点)）。🧪 = 実験的指定（`MAAI001`）。採用時は局所抑制が必要（**警告抑制は事前確認**：AGENTS.md）。

### 6.3 LLM プロバイダーの抽象化

- エージェントは**設定で選択した `IChatClient`** から `.AsAIAgent(...)` で生成する（接続生成を `ChatClients/` のファクトリに集約）。
- v1 の既定: **Foundry / Azure OpenAI**（`AzureOpenAIClient(Uri, ApiKeyCredential).GetChatClient(deployment)`）。
- 将来: **Ollama / Foundry Local**（OpenAI 互換エンドポイント経由で同じ `IChatClient` 抽象に載せる）。
- 設定キーは [§12](#12-設定一覧) の `Llm:*`。Foundry 互換の `Foundry__*` も受け付ける。

---

## 7. ストリーミング処理方針

> 本プロジェクトは**ストリーミング処理を基本**とする。

### 7.1 方針

- エージェント実行は `RunAsync`（一括）ではなく **`RunStreamingAsync`** を基本とし、`AgentResponseUpdate` を逐次受け取る（各 `ToString()` がテキスト断片、`Contents` に `FunctionCallContent` 等。F09 で確認済み）。
- ストリームを **UI 非依存の `AgentEvent` 列**へ変換するブリッジを `PcAgent.Agent/Streaming` に設ける（TUI サンプルの `IAgentConversation` / `AgentEvent` を踏襲）。

### 7.2 AgentResponseUpdate → AgentEvent 変換

| ストリーム上の内容 | 変換後イベント |
| --- | --- |
| テキスト断片 | `TextDelta(text)` |
| `FunctionCallContent` 検出 | `ToolCallStarted(name, args)` |
| ツール結果（`FunctionResultContent` 等） | `ToolCallCompleted(name, result)` |
| 推論/思考トークン（モデル対応時） | `ThinkingStarted` / `ThinkingDelta` / `ThinkingCompleted` |
| ストリーム終端 | `ResponseCompleted` |

> ツール呼び出しの厳密な検出はストリーム解析と**ツール呼び出しミドルウェア（4 引数 `.Use`）**の併用で確実化。思考トークンの扱いはモデル依存のため実装時に確定。

### 7.3 構造化出力との両立

- `RunAsync<T>`（構造化）は性質上ストリーミングと相反する。よって **診断レポート（構造化）はルール（決定的）で直接生成**し、LLM はストリーミングで説明・推奨を提供する設計を基本とする。`RunAsync<T>` は補助用途で別途ショーケース化（任意）。

### 7.4 CJK ストリーミングの分割

- 日本語の桁ずれ回避のため、TUI サンプルの `ResponseTokenizer` 相当（**CJK は 1 文字単位**、英数は単語単位、改行は独立トークン）で整形してから描画する。

---

## 8. CLI / TUI 設計

### 8.1 実行形態

- 対話 REPL（主用途）と非対話単発コマンド（[§4.1](#41-cli-基盤smartcommandlinehosting)）。REPL は「単発質問を連続で投げる」ことを中心に、`/コマンド`・`@`注入・`!`シェルを補助的に使う。

### 8.2 入力モデル（先頭 1 文字でモード分岐）

既存エージェント CLI 共通の「先頭文字でモード切替」を採用（付録 A）:

| 先頭 | モード | 例 | 補完 |
| --- | --- | --- | --- |
| `/` | スラッシュコマンド | `/diagnose` `/info cpu` `/help` | コマンド一覧をポップアップ・ファジー絞り込み・説明インライン表示 |
| `@` | 情報注入 | `@cpu` `@disk` `@smart` | 登録コレクタ名を補完。スナップショットをプロンプトに構造化注入 |
| `!` | シェル実行 | `!systeminfo` | **対応**。ユーザー起動は直接実行し出力取込（設定で無効化可）。LLM 起動のシェルは承認必須。`Actions:AllowShell` で制御 |
| それ以外 | 自然言語質問 | `CPU温度は？` | エージェントへ（ストリーミング回答） |

### 8.3 スラッシュコマンド一覧（初期案）

| コマンド | 機能 | 区分 |
| --- | --- | --- |
| `/help` | コマンド一覧・使い方 | 基本 |
| `/clear` | 画面/履歴クリア | 基本 |
| `/exit` `/quit` | 終了 | 基本 |
| `/info <category>` | 生情報の表示（cpu/gpu/mem/disk/smart/net/system/process…） | 情報取得 |
| `/diagnose [category]` | 診断実行（全体 or 範囲指定） | 診断 |
| `/health` | サブシステム健全性の一覧（✅/⚠️/❌ ダッシュボード） | 診断 |
| `/report [--save]` | 構造化診断レポートの生成（保存可） | 診断 |
| `/watch <metric>` | ライブ監視（`BarChart`/ゲージ） | 情報取得（任意） |
| `/actions` | 実行可能な修復アクション一覧 | アクション |
| `/clean <temp\|binobj>` | クリーンアップ（要承認） | アクション |
| `/rules [reload]` | 適用中のルール/閾値の表示・再読込 | 設定 |
| `/model` | モデルの表示/切替 | 設定 |
| `/config` | 設定の表示 | 設定 |
| `/status` `/context` | セッション・スナップショット概況 | 情報表示 |
| `/doctor` | エージェント自身の自己診断（LLM 接続・管理者権限・センサー可用性） | メタ |
| `/copy` `/save` | 直近応答のコピー/保存 | 会話 |

> ユーザー定義の `/コマンド` を Markdown で追加可能（[§13.3](#133-カスタムコマンドmarkdown)）。

### 8.4 コマンドサジェスト / 補完

- `/` 入力時に**候補ポップアップ**（ファジー絞り込み・説明と引数ヒントのインライン表示）。`Tab`/`Enter` 確定、`↑↓` 移動、`Esc` キャンセル。
- 実装候補は **PrettyPrompt**（C# 製の readline + ポップアップ補完）。Spectre.Console は出力に用い、入力＝PrettyPrompt の役割分担。**入出力の協調・CJK 挙動は [§9](#9-spectreconsole--ストリーミングの検証計画) で検証**し、不可なら自前の簡易補完にフォールバック。

### 8.5 表示モデル（イベント駆動・ストリーミング）

- TUI は `AgentEvent` 列を購読して描画（UI と LLM 実装を分離）。状態（思考中／ツールカード／本文／トークン数／完了）を保持し、イベント適用ごとに再描画。

### 8.6 画面要素（リッチ装飾・**罫線不使用**）

| 要素 | 用途 | Spectre コンポーネント |
| --- | --- | --- |
| 起動バナー | ロゴ/タイトル/ヒント | ASCII アート + 配色 |
| 収集進捗 | 「CPU 収集… GPU… ディスク…」 | `Progress` / `Status`（スピナー） |
| 思考中 | スピナー + 直近思考 | `Live` + ブライユ点字スピナー |
| ツール呼び出し | 名前/引数/結果（折りたたみ/展開） | `Markup`（行頭マーカー） |
| 本文 | ストリーミング応答 | `Live` または逐次 `Write`（[§9](#9-spectreconsole--ストリーミングの検証計画) で確定） |
| 診断ダッシュボード | 部位ごとの健全性一覧 | **枠なしテーブル**（`TableBorder.None`）+ 絵文字ステータス（✅/⚠️/❌）、温度・使用率は `BarChart` |
| リソース可視化 | 使用率バー/ゲージ | `BarChart` + 色（緑/黄/赤） |
| 重要指摘 | 警告/危険のハイライト | **枠は使わず**、色（黄/赤）+ 絵文字 + インデント（`Panel` 枠は不使用） |

- **装飾方針（確定）**: 日本語表示を優先し、**罫線（テーブル/パネルの枠線）は使用しない**。一方で**絵文字・進捗グラフ（`BarChart` / `Progress`）は使用する**。枠を使わないことで CJK 端末の枠ずれ（曖昧幅による右枠ずれ）を原理的に回避（TuiSpectre サンプルも応答を枠なしで描画）。見た目に問題が出る箇所は個別に修正（ASCII 代替・幅 1 安全記号へ差替え）。適性は [§9](#9-spectreconsole--ストリーミングの検証計画) で実機検証。

### 8.7 CJK / 配色

- 罫線は使わず、装飾記号は原則 **ASCII**（`> - | ...`）+ **絵文字**（ステータス/見出し等）。幅 1 で安全な記号（ブロックアート・ブライユスピナー）は可。曖昧幅で崩れる箇所は差替え。
- 配色（暗背景前提）: 応答本文=白（bold 回避）、コード=aqua、アプリ的出力/記号=silver、役割見出し=green/aqua/blue、思考・状態=yellow。`grey`(#808080) は不使用。`Markup.Escape` を入力・外部値に必ず適用。

### 8.8 割り込み・複数行・履歴

- **`Esc`=生成中断（セッション維持）**、**`Ctrl+C` 2 回=終了**、`Ctrl+D`=EOF 終了。`↑↓`/`Ctrl+R` で履歴。複数行は `Ctrl+J`（端末非依存）を基本（既存 CLI 共通則・付録 A）。
- 生成中断は `RunStreamingAsync` に渡す `CancellationToken` で実現。

### 8.9 リダイレクト時のフォールバック

- 入力リダイレクト時は補完/プロンプトを避け `Console.ReadLine`。出力リダイレクト時は `Live` を避け、全イベント集約後に 1 回描画。ヘッドレス/パイプでも最低限動作。

---

## 9. Spectre.Console + ストリーミングの検証計画

> Spectre.Console（+ 入力補完）で問題がないかを**実機検証**する。診断応答は長文化し得るため、`Live` の適性と入力補完の協調を事前確認する。

### 9.1 検証する懸念点

| # | 懸念 | 内容 |
| --- | --- | --- |
| C1 | **ビューポート超過** | `Live` 対象が端末高を超えると描画破綻/ジャンプ/切れ（長い診断本文で顕在化・最重要） |
| C2 | 再描画コスト | `Live.UpdateTarget` は毎回全体再構築。長文ストリームで遅延/カクつき |
| C3 | ちらつき | 高頻度トークン更新のフリッカ |
| C4 | CJK/絵文字幅 | 絵文字・曖昧幅の表示幅ずれ（**枠線不使用のためテーブル枠ずれは回避**。残るは行内絵文字・`BarChart` ラベル・`Live` 行折り返し） |
| C5 | 部分マークアップ破綻 | ストリーム断片が `[` 等を分断（→ 完了時のみ整形・途中は素テキスト+Escape で回避） |
| C6 | リダイレクト | パイプ/ヘッドレス時のフォールバック |
| C7 | **入力補完の協調** | PrettyPrompt（入力・補完）と Spectre.Console（出力・`Live`）の併用可否、`/` ポップアップの CJK 挙動 |

### 9.2 検証方法（小規模 PoC）

- 3,000〜5,000 文字の**日本語長文**をトークン単位で `Live` に投入し C1〜C3 を計測（描画時間/総時間/フリッカ/端末高超過時の挙動）。
- 枠なしレイアウトに絵文字・日本語を混在させ、`BarChart`・進捗・行内絵文字の表示幅を実機（日本語 Windows Terminal）で確認（C4）。
- PrettyPrompt で `/` 補完ポップアップ + 複数行入力を試し、Spectre 出力との切り替え・CJK 入力を確認（C7）。
- 入出力リダイレクトで C6 を確認。

### 9.3 判定基準と代替策

- **推奨デフォルト（C1 回避設計）**: 一過性 UI（思考スピナー・収集進捗・ツール状態）は `Live`/`Status`/`Progress`（高さ限定）で描き、**確定した応答本文は通常のスクロールバックへ逐次 `Write`** する。ビューポート超過を原理回避しつつストリーミング表示を実現。
- C4 が破綻する場合: 装飾を ASCII 化／絵文字を幅 1 安全記号に限定／問題セルを差替え。
- C7 が不可の場合: PrettyPrompt をやめ、自前の簡易補完（候補一覧を Spectre で下部表示）にフォールバック。
- 最終手段（C1 が本質的に不可）: スクロール可能なトランスクリプトを持つ **Terminal.Gui** への切替（コスト大のため最後）。
- **本検証の結論で [§8.6](#86-画面要素リッチ装飾罫線不使用) の本文表示方式・[§8.4](#84-コマンドサジェスト--補完) の補完方式を確定する。**

### 9.4 検証結論（P4 で確定）

- **本文表示方式 = スクロールバック逐次出力に確定**。Spectre `Live` は使わない（→ C1 ビューポート超過・C2 再描画コスト・C3 ちらつきを**原理的に回避**）。構造化表示（info / 後続の診断ダッシュボード）は `AnsiConsole.Write/MarkupLine` による**静的描画**で行う。これによりリダイレクト/ヘッドレスでも安全に動作（C6 解消）。
- 実装・確認済み（P4）: 起動バナー（`FigletText`）、`info` のリッチ描画（絵文字ヘッダ・色・**進捗バー `█░`**）、`--ask` のストリーミング表示（色付きラベル + 本文の逐次出力）。リダイレクト環境で描画パイプライン動作を確認。
- C4（絵文字/CJK 幅）: **罫線・桁揃えテーブルを使わず**「`ラベル: 値` 行 + 幅 1 のブロックバー」構成にしたため整列崩れは原理的に発生しない。絵文字は非整列のヘッダ位置のみ。最終的な見た目は**ユーザーの実ターミナルでの確認を推奨**（問題があれば §8.6 方針で差替え）。
- C5（部分マークアップ破綻）: ストリーム本文は `Markup.Escape` で都度エスケープし `[white]…[/]` で出力（途中整形なし）。
- C7（PrettyPrompt と Spectre の協調）: 入力補完は対話 REPL の要素のため **P5 で実機検証**（不可なら Builtin 補完へフォールバック）。本環境は非対話のため P4 では未検証。
- 思考スピナー/`Status` 等の一過性 UI は対話 REPL（P5）で `Console.IsOutputRedirected` ガード付きで導入する。

---

## 10. 可観測性・テレメトリ（OTEL）

- 2 層の処理時間ログ: **CLI レベル**＝Smart.CommandLine の `ICommandFilter`（コマンド全体の時間・例外）、**エージェントレベル**＝Agent Framework ミドルウェア（実行全体 5 引数 / 各ツール 4 引数）。
- 計装は `AsBuilder().UseOpenTelemetry(sourceName, t => t.EnableSensitiveData = ...)`。
- **重要な差分**: 参考リポジトリ（Feature08）は `ActivityListener` でコンソール出力するのみで OTLP/メトリクス配線が無い。構想の「OTEL コレクター送信」を満たすため本プロジェクトで追加:
  - `OpenTelemetry` / `OpenTelemetry.Exporter.OpenTelemetryProtocol` 追加。
  - `TracerProvider` 構築 + **`AddSource(sourceName)`** + `AddOtlpExporter(endpoint)`。
  - 必要に応じ `MeterProvider` + カスタム `Meter`（収集件数・診断重大度・処理時間ヒストグラム）。
- **OTLP 送信は既定オフ**（`Telemetry:Otlp:Enabled=false`）、設定で有効化。送信先は **OpenTelemetry Collector** および **Aspire Dashboard**（OTLP 受信）。
- **OTel 構成は `PcAgent.Agent` 内に実装**する（Aspire の `ServiceDefaults` プロジェクトは作らない）。開発・可視化用に **Aspire AppHost プロジェクトを別途用意するのは任意**（アプリ本体はこれに依存しない）。
- `EnableSensitiveData` は**既定オフ**（個人 PC 情報・プロンプト/応答の混入注意）。

---

## 11. 非機能要件

| 区分 | 要件 |
| --- | --- |
| ビルド品質 | **警告ゼロ**（`AnalysisMode=All` / `WarningsAsErrors=nullable`）。警告抑制が必要な場合は**事前相談**（AGENTS.md）。 |
| コーディング規約 | `.editorconfig` 準拠。**メンバ変数に `_` プレフィックスを付けない**。数値整形は `CultureInfo.InvariantCulture` + `string.Create`（CA1305/1304）。 |
| 実験的 API | `MAAI001` 使用箇所のみ局所 `#pragma` 抑制（採用は §6.2 の判断後）。 |
| 権限 | センサー/SMART 取得は**管理者権限が必要**。非管理者時は明示メッセージで縮退（取得可能な範囲のみ）。`/doctor` で権限を確認。 |
| スレッド安全性 | センサー更新と読取の競合に注意（PrometheusExporter は `Lock` + `Interlocked` + 節流）。 |
| 性能 | 収集は節流（既定 1 秒程度）。SMART は別間隔（例 10 秒）。 |
| 文字コード | コンソール UTF-8。 |
| 機密情報 | API キーは user-secrets / 環境変数（`Llm__*` / `Foundry__*`）。コミット禁止。トレースへの PII 混入注意。 |
| 配布 | 単一ファイル発行（SelfContained / SingleFile）。**Trimming は行わない**（`PublishTrimmed=false`。LibreHardwareMonitor 等の互換性のため）。 |

---

## 12. 設定一覧

`appsettings.json`（Smart.CommandLine の `UseDefaults` が読込）→ user-secrets → 環境変数（区切り `__`）の順で解決。

| キー | 型 | 既定 | 説明 |
| --- | --- | --- | --- |
| `Llm:Provider` | enum | `Foundry` | `Foundry` / `Ollama` / `FoundryLocal` |
| `Llm:Endpoint` | string | — | プロバイダーのエンドポイント |
| `Llm:ApiKey` | string | — | API キー（user-secrets/env 推奨。`Foundry__ApiKey` 互換） |
| `Llm:Model` | string | `gpt-5.4-mini` | デプロイメント/モデル名 |
| `Collection:UpdateIntervalMs` | int | `1000` | センサー収集の節流間隔 |
| `Collection:SmartIntervalMs` | int | `10000` | SMART 収集間隔 |
| `Collection:Enabled:<Category>` | bool | `true` | カテゴリ別の収集有効/無効（Cpu/Gpu/Memory/…/Wifi） |
| `Diagnostics:ThresholdsPath` | string | `rules/thresholds.json` | 閾値ファイル（外部化・ホットリロード） |
| `Diagnostics:RulesPath` | string | `rules/rules.json` | ルールファイル（外部化） |
| `Rag:Enabled` | bool | `true` | RAG 文脈注入の有効化 |
| `Rag:KnowledgePath` | string | `knowledge/` | ナレッジ（ポリシー/対処）格納先 |
| `Actions:Enabled` | bool | `true` | 修復アクションの有効化 |
| `Actions:RequireApproval` | bool | `true` | 修復前の HITL 承認を必須化 |
| `Actions:AllowShell` | bool | `true` | `!` シェル実行の許可（ユーザー起動。無効化可） |
| `Actions:BinObj:Roots` | string[] | （なし） | bin/obj 探索ルート。**既定値は持たず毎回明示指定** |
| `Actions:BinObj:Exclude` | string[] | — | 除外パターン |
| `Telemetry:Otlp:Enabled` | bool | `false` | OTLP 送信の有効化 |
| `Telemetry:Otlp:Endpoint` | string | `http://localhost:4317` | 送信先（OpenTelemetry Collector / Aspire Dashboard の OTLP 受信） |
| `Telemetry:EnableSensitiveData` | bool | `false` | プロンプト/応答をトレースに含める |
| `Ui:DecorationLevel` | enum | `Rich` | `Rich`（絵文字/グラフ多用）/ `Safe`（ASCII 中心） |
| `Ui:StreamBodyToScrollback` | bool | `true` | 本文をスクロールバックへ逐次出力（[§9.3](#93-判定基準と代替策) の推奨） |
| `Ui:CompletionEngine` | enum | `PrettyPrompt` | `PrettyPrompt` / `Builtin`（自前簡易補完） |
| `Customization:CommandsPaths` | string[] | `~/.pcagent/commands`, `.pcagent/commands` | カスタム `/コマンド`（プロジェクト優先） |
| `Logging:*` | — | — | 標準 `Microsoft.Extensions.Logging` 設定 |

> ※ キー名・既定値はドラフト。実装時に微調整。

---

## 13. 外部ファイル仕様（ルール・閾値・カスタムコマンド）

> 「ルールは完全な外部ファイル化により変更可能に」「診断閾値も外部ファイル化」の方針を満たす。形式は **JSON**（`Microsoft.Extensions.Configuration` と親和。`reloadOnChange` でホットリロード）を基本案とする（YAML 採用可否は [§15](#15-要確認事項)）。

### 13.1 診断閾値（thresholds）

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

### 13.2 診断ルール（rules）

ルール = 「対象メトリクス・条件（演算子＋閾値参照）・重大度・メッセージ・推奨アクション」。v1 は比較ベースで十分。

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

- エンジンはスナップショットの**メトリクスパス**（`storage.lifeRemainingPercent` 等）を解決し、演算子で評価。`/rules reload` で再読込。

### 13.3 カスタムコマンド（Markdown）

既存エージェント CLI 共通の **Markdown + frontmatter** 形式（付録 A）。`Customization:CommandsPaths`（グローバル/プロジェクトの 2 層、プロジェクト優先）に配置。

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

- `@<collector>` で情報注入、`` !`cmd` `` でシェル出力注入（`Actions:AllowShell` に従う）、`$ARGUMENTS`/`$1` で引数展開。

---

## 14. 段階的実装方針

> 実装は「仕様 Fix → 実装プラン作成 → 実装」の順。本章は大枠（プランで詳細化）。

1. **CLI 基盤**: Smart.CommandLine.Hosting でソリューション/プロジェクト雛形（BleScan 準拠）、`Directory.Build.props` 継承、`UseDefaults`、グローバルフィルタ（実行時間/例外）。`info`/`diagnose` の単発コマンド骨格。
2. **情報取得**: `PcAgent.Diagnostics`（Collectors: HW/SMART/System）+ 拡張ポイント（`ICollector`）。
3. **エージェント + ストリーミング**: `IChatClient` 抽象 + Foundry 実装、関数ツール、`RunStreamingAsync` → AgentEvent ブリッジ、最小 TUI。
4. **TUI + Spectre 検証**: 描画（枠なし・絵文字・BarChart）、入力補完（PrettyPrompt）、[§9](#9-spectreconsole--ストリーミングの検証計画) の PoC を実施し本文表示方式・補完方式を確定。
5. **REPL / スラッシュコマンド / サジェスト**: `/ @ !` ディスパッチ、コマンド一覧と補完、`/info` `/diagnose` `/health` 等。
6. **診断ロジック**: 外部ルール/閾値エンジン + 構造化レポート + 診断ダッシュボード描画。
7. **RAG / ContextProvider**: ポリシー注入。
8. **HITL アクション**: 承認フロー + 一時ファイル/bin・obj クリーンアップ。
9. **可観測性**: 処理時間ログ（CLI/エージェント 2 層）+ OTEL/OTLP（既定オフ）。
10. **カスタムコマンド・オプション機能**: Markdown コマンド、評価/圧縮等を段階追加。

各フェーズ末で**警告ゼロのビルド**を確認する。

---

## 15. 要確認事項

v0.3 で**全項目が確定**しました。本仕様は実装プラン着手可能な状態です。

### 確定事項（一覧）

| 論点 | 決定 |
| --- | --- |
| LLM プロバイダー | `IChatClient` 抽象。当面 Foundry/Azure OpenAI、Ollama / Foundry Local も視野 |
| マルチエージェント | 不採用 |
| アクション | 一時ファイル削除 + VS の bin/obj 削除（**探索ルートは毎回明示**）+ **`!` シェル実行対応** |
| 外部ファイル形式 | **JSON**（ルール・閾値・ナレッジ）。閾値は**仮値で着手**し外部ファイルで調整 |
| OTLP 送信先 | OpenTelemetry Collector / Aspire Dashboard。既定オフ・設定で有効化 |
| テレメトリ構成 | **Agent プロジェクト内に実装**（`ServiceDefaults` は作らない。Aspire AppHost は任意） |
| 入力補完 | **PrettyPrompt** 採用（最終可否は §9 PoC） |
| 配布 | 単一ファイル・**Trimming なし** |
| プロジェクト構成 | 3 分割（WiFi 等の収集拡張前提） |
| 対話形態 | 単発質問中心 + `/コマンド` + `@`注入 + `!`シェル + サジェスト。情報取得も一級対応 |
| 装飾 | 日本語優先・罫線不使用・絵文字/進捗グラフ使用 |

---

## 付録 A: 参考にしたエージェント CLI と採用機能

著名 OSS エージェント CLI（Claude Code / Gemini CLI / Codex CLI / Aider / Goose / OpenCode / Crush / Cline / Continue / Qwen Code / Cursor / Amazon Q）を横断調査し、本エージェントに採り入れる機能を選定した（「一般的な AI エージェント CLI 実装の実験」の意図）。

| 優先度 | 採用機能 | 主な出典 |
| --- | --- | --- |
| P0 | 先頭 1 文字モード分岐（`/` `@` `!`） | 全 CLI 共通 |
| P0 | `/` ポップアップ補完 + ファジー + 説明/引数ヒント | Claude Code, Gemini CLI |
| P0 | 診断特化コマンド（`/doctor` `/diagnose` `/health` `/report`） | Claude `/doctor` `/debug`, Codex `/debug-config`, Goose diagnostics |
| P0 | リソース可視化（使用率バー/ゲージ/色） | Amazon Q `/usage`, Claude `/context`, Goose ドットゲージ |
| P0 | `Esc`=中断 / `Ctrl+C` 2 回=終了 / `Ctrl+J` 複数行 / ストリーミング・折りたたみ | 全 CLI 共通 |
| P1 | 集約ダッシュボード（健全性・状態の一望） | Crush サイドバー |
| P1 | 読取専用 ⇄ 修復のモード分離 + 承認プロンプト（今回のみ/常に/中止） | Claude, Gemini, Codex |
| P1 | `@` 情報プロバイダー（`@cpu` `@disk` `@smart` …） | Cline `@problems` 他 |
| P1 | Markdown カスタムコマンド（2 層・frontmatter・`$ARGUMENTS`・`` !`cmd` ``・`@path`） | Claude, Gemini, OpenCode 他 |
| P2 | 終了時サマリ（発見問題数・確認項目・所要時間） | Gemini CLI, Qwen |
| P2 | `/compact`・自動コンパクション（長時間セッション） | Goose, Cline, Amazon Q |

> 実装ライブラリ参考: 補完＝**PrettyPrompt**（C#）、出力＝**Spectre.Console**。詳細出典は調査ログ参照。

---

> 次アクション: 本 v0.2 のレビュー → [§15](#15-要確認事項) を中心に修正 → Fix 後に**実装プラン**を作成します。
