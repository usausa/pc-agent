# PcAgent

Windows PC の情報取得・診断・修復を行うエージェント CLI。情報収集(ハードウェア/SMART/システム)、外部ルールによる決定的な診断、LLM との対話(ストリーミング)、承認付きの修復アクションを 1 つの TUI に統合する。

- **基盤**: .NET 10 / C# / [Smart.CommandLine.Hosting](https://www.nuget.org/packages/Usa.Smart.CommandLine.Hosting)
- **エージェント**: Microsoft Agent Framework (`Microsoft.Agents.AI`)
- **TUI**: [Spectre.Console](https://spectreconsole.net/) + [PrettyPrompt](https://github.com/waf/PrettyPrompt)（罫線なし・絵文字/グラフ・日本語優先）

## 前提

- **OS**: Windows（TFM は `net10.0-windows`）。
- **.NET SDK**: .NET 10。
- **管理者権限(推奨)**: 一部のハードウェアセンサー・SMART 情報の取得には管理者権限が必要。権限が無い場合は取得可能な範囲に縮退する（`/doctor` で可用性を確認）。
- **LLM 接続(任意)**: 対話・RAG・承認付きアクションには LLM 接続が必要。未設定でも情報取得・診断は動作する。

## ビルドと実行

```pwsh
dotnet build PcAgent.slnx

# 単発コマンド
dotnet run --project PcAgent.Tui -- info <category>   # 例: info cpu
dotnet run --project PcAgent.Tui -- diagnose

# 引数なしで対話(REPL)起動
dotnet run --project PcAgent.Tui
```

## 使い方

### 単発コマンド

| コマンド | 説明 |
| --- | --- |
| `info <category>` | 指定カテゴリ(cpu/gpu/memory/disk/smart/battery/system)の情報を表示 |
| `diagnose` | 外部ルールで診断し、指摘を表示 |

### 対話(REPL)

先頭文字でモードを切り替える。

| 入力 | 動作 |
| --- | --- |
| `/<command>` | スラッシュコマンド（`/help` で一覧） |
| `@<category> [質問]` | 情報取得（質問を続けるとエージェントへ注入） |
| `!<command>` | シェル実行（`Actions:AllowShell` に従う） |
| その他のテキスト | エージェントへの質問 |

主なスラッシュコマンド: `/help` `/info` `/diagnose` `/health` `/report [save]` `/rules` `/actions` `/clean temp|binobj <root>` `/status` `/doctor` `/model` `/clear` `/exit`。

## 設定

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

### 可観測性(OpenTelemetry)

`Telemetry:Otlp:Enabled=true` で OTLP エクスポートを有効化（既定オフ）。送信先は `Telemetry:Otlp:Endpoint`（既定 `http://localhost:4317`）。OpenTelemetry Collector / Aspire Dashboard で受信する。無効時は処理時間のローカルログのみ。

## カスタムコマンド

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

## 発行(単一ファイル)

自己完結・単一ファイル・**Trimming なし**（LibreHardwareMonitor 互換性のため）。

```pwsh
dotnet publish PcAgent.Tui -p:PublishProfile=win-x64
```

`PcAgent.Tui/bin/Release/net10.0-windows*/win-x64/publish/` に `PcAgent.Tui.exe` と設定ファイル(`appsettings.json` / `rules/` / `knowledge/`)が出力される。

## プロジェクト構成

| プロジェクト | 役割 |
| --- | --- |
| `PcAgent.Diagnostics` | 情報収集(Collectors)・診断ルールエンジン・修復サービス |
| `PcAgent.Agent` | エージェント配線(ツール/RAG/承認/計測)・LLM 抽象 |
| `PcAgent.Tui` | CLI / REPL / 描画(Spectre)・カスタムコマンド |

詳細仕様は [`docs/spec.md`](docs/spec.md)、実装プランは [`docs/plan.md`](docs/plan.md) を参照。
