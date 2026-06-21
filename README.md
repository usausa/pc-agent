# 🖥️ PcAgent

Windows PC の情報取得・診断・修復を行うエージェント CLI。情報収集(ハードウェア/SMART/システム)、外部ルールによる決定的な診断、LLM との対話(ストリーミング)、承認付きの修復アクションを 1 つの TUI に統合する。

- **基盤**: .NET 10 / C# / [Smart.CommandLine.Hosting](https://www.nuget.org/packages/Usa.Smart.CommandLine.Hosting)
- **エージェント**: Microsoft Agent Framework (`Microsoft.Agents.AI`)
- **TUI**: [Spectre.Console](https://spectreconsole.net/) + [PrettyPrompt](https://github.com/waf/PrettyPrompt)（日本語優先・絵文字/進捗グラフ・桁ずれしないハイフン区切り）

## ✨ 主な機能

- **情報取得**: ハードウェア(CPU/GPU/メモリ/ディスク)・SMART・システム情報の収集（`ICollector` で拡張可能）。
- **診断**: 外部 JSON ルール/閾値による決定的な診断とダッシュボード表示（実行ごとに再読込）。
- **対話エージェント**: LLM とのストリーミング対話、ツール呼び出しの可視化、ナレッジ(RAG)注入、簡易マークダウン整形。
- **REPL**: `/` コマンド・`@` 情報注入・`!` シェルのモード切替、補完(PrettyPrompt)、終了サマリ。
- **カスタムコマンド**: Markdown(frontmatter + `$ARGUMENTS`/`@`/`` !`cmd` ``)で `/コマンド` を追加。
- **修復アクション(HITL)**: 一時ファイル / bin・obj 削除を承認付きで実行。
- **可観測性**: 処理時間ログ + OpenTelemetry トレース/OTLP（既定オフ）。
- **配布**: 自己完結・単一ファイル(Trimming なし)発行。

### 🤖 使用している Microsoft Agent Framework 機能

Microsoft Agent Framework（`Microsoft.Agents.AI` 1.10.0）の機能カタログに沿った採用状況。**✅=使用中** / **🔜=使用予定**（[`docs/plan.md`](docs/plan.md)）。

| カテゴリ | 機能 | 状態 | PcAgent での実装 / API |
| --- | --- | :--: | --- |
| 🚀 基礎・実行 | エージェント生成 | ✅ | `chatClient.AsAIAgent(ChatClientAgentOptions)` |
| | ストリーミング | ✅ | `RunStreamingAsync` → `AgentEvent`（`FunctionCallContent`/`FunctionResultContent`/テキスト） |
| | セッション | ✅ | `CreateSessionAsync`（HITL 承認の中断 → 再開） |
| 🔧 ツール | 関数ツール | ✅ | `AIFunctionFactory.Create`（`PcInfoTools.GetPcInfo`/`ListCategories`） |
| | ツール承認 (HITL) | ✅ | `ApprovalRequiredAIFunction` + `ToolApprovalRequestContent` + `CreateResponse`（`MaintenanceTools`） |
| 🧠 コンテキスト・メモリ | コンテキストプロバイダー | ✅ | `AIContextProvider`/`AIContext`（`PcContextProvider`: 時刻・権限・最逼迫ドライブ） |
| | RAG / テキスト検索 | ✅ | `TextSearchProvider` + `TextSearchProviderOptions`（`KnowledgeStore`） |
| | 履歴の圧縮 (Compaction) | 🔜 | `CompactionProvider`（plan.md §5・実験的 `MAAI001`） |
| ⚙️ ミドルウェア | ミドルウェア + ロギング | ✅ | `AsBuilder().Use(...)`（実行 5 引数 / ツール 4 引数）+ `LoggerMessage` 計測 |
| 📊 可観測性・評価 | テレメトリ (OpenTelemetry) | ✅ | `UseOpenTelemetry` + OTel `TracerProvider`/OTLP（既定オフ） |
| | メトリクス | 🔜 | `MeterProvider`（plan.md §3） |
| | 評価（ローカル検査） | 🔜 | `LocalEvaluator`（plan.md §4） |
| 🔌 相互運用・プロバイダー | マルチプロバイダー | ✅ | Foundry=`AzureOpenAIClient` / Ollama・FoundryLocal=`OpenAIClient` を共通 `AIAgent` に |
| | DI 連携 | ✅ | `AddPcAgent`（`Microsoft.Extensions.DependencyInjection`） |

> 未採用の主な機能（参考）: 構造化出力・MCP/ホスト型ツール・ベクトル/ファイル記憶・TODO/スキル・マルチエージェント（ツール化/ワークフロー）・A2A など。本ツールの用途では不要。

## 📋 前提

- **OS**: Windows（TFM は `net10.0-windows`）。
- **.NET SDK**: .NET 10。
- **管理者権限(推奨)**: 一部のハードウェアセンサー・SMART 情報の取得には管理者権限が必要。権限が無い場合は取得可能な範囲に縮退する（`/doctor` で可用性を確認）。
- **LLM 接続(任意)**: 対話・RAG・承認付きアクションには LLM 接続が必要。未設定でも情報取得・診断は動作する。

## 🚀 ビルドと実行

```pwsh
dotnet build PcAgent.slnx

# 単発コマンド
dotnet run --project PcAgent.Tui -- info <category>   # 例: info cpu
dotnet run --project PcAgent.Tui -- diagnose

# 引数なしで対話(REPL)起動
dotnet run --project PcAgent.Tui
```

## 📖 使い方

### ⌨️ 単発コマンド

| コマンド | 説明 |
| --- | --- |
| `info <category>` | 指定カテゴリ(cpu/gpu/memory/disk/smart/battery/system)の情報を表示 |
| `diagnose` | 外部ルールで診断し、指摘を表示 |
| `--ask "<質問>"` | 単発質問をストリーミング表示して終了 |

### 💬 対話(REPL)

先頭文字でモードを切り替える。

| 入力 | 動作 |
| --- | --- |
| `/<command>` | スラッシュコマンド（`/help` で一覧） |
| `@<category> [質問]` | 情報取得（質問を続けるとエージェントへ注入） |
| `!<command>` | シェル実行（`Actions:AllowShell` に従う） |
| その他のテキスト | エージェントへの質問 |

#### 📑 スラッシュコマンド一覧

| コマンド | 説明 |
| --- | --- |
| `/help` | コマンド一覧を表示 |
| `/info <category>` | PC 情報を表示（cpu/gpu/memory/disk/smart/battery/system） |
| `/diagnose` | 診断を実行して指摘を表示 |
| `/health` | 健全性の概況（crit/warn/info の件数） |
| `/report [save]` | 診断レポートを生成（`save` で JSON 保存） |
| `/rules [reload]` | ルール/閾値の状態を表示（実行ごとに再読込） |
| `/actions` | 実行可能なアクション一覧 |
| `/clean temp` | 一時ファイルを削除（列挙 → 確認 → 削除） |
| `/clean binobj <root>` | 指定ルート配下の bin/obj を削除（確認後・`.csproj` 近傍のみ） |
| `/status` | セッションの概況を表示 |
| `/config` | 現在の設定を表示 |
| `/doctor` | 自己診断（接続/権限/コレクタ） |
| `/model` | 使用モデルを表示 |
| `/clear` | 画面をクリア |
| `/exit` | 終了（セッション概要を表示） |

> カスタムコマンドを置くと、この一覧に `/コマンド` が追加されます（[カスタムコマンド](#-カスタムコマンド)）。

入力の先頭文字以外（自然文）はエージェントへの質問になります。生成中は `Esc` で中断（セッションは維持）、`Ctrl+C` 2 回で終了します。

## ⚙️ 設定

`PcAgent.Tui/appsettings.json`（ユーザーシークレット・環境変数で上書き可。API キーはシークレット/環境変数推奨）。

| セクション | キー | 説明 |
| --- | --- | --- |
| `Llm` | `Provider` / `Endpoint` / `ApiKey` / `Model` | LLM 接続（`Foundry` / `Ollama` / `FoundryLocal`） |
| `Diagnostics` | `ThresholdsPath` / `RulesPath` | 診断ルール・閾値ファイル |
| `Rag` | `Enabled` / `KnowledgePath` | ナレッジ注入 |
| `Actions` | `Enabled` / `RequireApproval` / `AllowShell` | 修復アクションの可否 |
| `Telemetry` | `EnableSensitiveData` / `Otlp:Enabled` / `Otlp:Endpoint` | OTLP 送信（既定オフ） |
| `Customization` | `CommandsPaths` | カスタムコマンドの探索パス |

LLM を Foundry で使う例（ユーザーシークレット）:

```pwsh
dotnet user-secrets --project PcAgent.Tui set "Llm:Endpoint" "https://<resource>.openai.azure.com/"
dotnet user-secrets --project PcAgent.Tui set "Llm:ApiKey" "<key>"
```

### 📊 可観測性(OpenTelemetry)

`Telemetry:Otlp:Enabled=true` で OTLP エクスポートを有効化（既定オフ）。送信先は `Telemetry:Otlp:Endpoint`（既定 `http://localhost:4317`）。OpenTelemetry Collector / Aspire Dashboard で受信する。無効時は処理時間のローカルログのみ。

ローカルで可視化する例（Aspire Dashboard）:

```pwsh
docker run --rm -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
$env:Telemetry__Otlp__Enabled='true'; dotnet run --project PcAgent.Tui -- diagnose
```

## 🧩 カスタムコマンド

`Customization:CommandsPaths`（既定 `~/.pcagent/commands`、`.pcagent/commands`。プロジェクト優先）に Markdown を置くと `/コマンド` が増える。

```markdown
---
name: pc-check
description: PC の基本状態をまとめて確認する
argument-hint: [メモ]
---
次の情報を確認し、問題があれば指摘してください。メモ: $ARGUMENTS

@memory
@system

ホスト情報:
!`hostname`
```

- `$ARGUMENTS` / `$1`…: 引数を展開。
- `@<category>`: コレクタ情報を注入。
- `` !`cmd` ``: シェル出力を注入（`Actions:AllowShell` に従う）。

展開結果はエージェントへ送られる（LLM 未設定時は展開結果のみ表示）。

### 📁 配置と管理

- **2 層**: `~/.pcagent/commands`（ユーザーごと・ホーム配下、インストール場所に依存せず常に有効）と `<起動時のカレントディレクトリ>/.pcagent/commands`（プロジェクトごと・exe の場所ではなくカレント基準）。
- **発行物には同梱されない**（配布物は exe + `appsettings.json` + `rules/` + `knowledge/`）。利用者が上記の場所に後から配置する。
- **ソース管理**: 個人用は追跡しない（本リポジトリは `.pcagent/` を gitignore 済み）。チームで共有したいプロジェクトコマンドはそのプロジェクト側でコミット可。
- **形式の見本**: [`examples/commands/pc-check.md`](examples/commands/pc-check.md)。

## 📦 発行(単一ファイル)

自己完結・単一ファイル・**Trimming なし**（LibreHardwareMonitor 互換性のため）。

```pwsh
dotnet publish PcAgent.Tui -p:PublishProfile=win-x64
```

`PcAgent.Tui/bin/Release/net10.0-windows*/win-x64/publish/` に `PcAgent.Tui.exe` と設定ファイル(`appsettings.json` / `rules/` / `knowledge/`)が出力される。

## 🗂️ プロジェクト構成

| プロジェクト | 役割 |
| --- | --- |
| `PcAgent.Diagnostics` | 情報収集(Collectors)・診断ルールエンジン・修復サービス |
| `PcAgent.Agent` | エージェント配線(ツール/RAG/承認/計測)・LLM 抽象 |
| `PcAgent.Tui` | CLI / REPL / 描画(Spectre)・カスタムコマンド |

詳細仕様は [`docs/spec.md`](docs/spec.md)、残作業（優先度つき）は [`docs/plan.md`](docs/plan.md) を参照。
