# 🗒️ PC 診断・情報取得エージェント 残作業

> ステータス: 実装フェーズ **P0–P10 完了**（2026-06-21）。LLM 接続を含む実機検証まで実施済み。
> 実装済み機能の詳細は [`README.md`](../README.md)、設計は [`spec.md`](spec.md) を参照。
> 本書は残りの **要否判断・任意拡張・未実施の動作確認** のみを扱う（実装済みフェーズの記述は削除済み）。

---

## ✅ 完了済み（概要）

- **情報取得 / 診断**: Collectors（HW/SMART/System）、外部 JSON ルール/閾値エンジン、ダッシュボード表示。
- **エージェント**: ストリーミング対話、関数ツール、RAG/コンテキスト注入、HITL 承認、2 層処理時間ログ、OpenTelemetry/OTLP（既定オフ）。
- **LLM シェルツール**: 承認(HITL) + 許可リスト(`Actions:Shell:AllowedCommands`) + シェル演算子拒否で制限（実機検証済み）。
- **TUI / REPL**: `/ @ !` ディスパッチ、補完、カスタムコマンド（Markdown）、終了サマリ、簡易マークダウン整形。
- **配布**: 自己完結・単一ファイル（Trimming なし）発行。
- **実機検証済み**: 情報取得・診断・REPL・カスタムコマンド・`/clean`（承認ゲート）・OTLP 初期化・単一 exe、および LLM 依存経路（対話 / ツール / RAG / HITL 承認 / 層 2 ログ）。

> 使用中の Agent Framework 機能は [`README.md`](../README.md) の機能表（✅）を参照。下記 🔜 は**使用予定**（未実装）の機能。

---

## 🚧 残作業（優先度順）

> 優先度: **★★★=高** / **★★=中** / **★=低**（重要度と着手価値の総合）。
> 🔜 = 使用予定の Agent Framework 機能（README 機能表の 🔜 と対応）。

| 優先度 | 項目 | 種別 | 使用予定 AF 機能 | 目安工数 |
| --- | --- | --- | --- | --- |
| ★★ | 2. OTLP コレクタ受信確認 | 動作確認 | — | 小 |
| ★★ | 3. メトリクス | 任意 | 🔜 `MeterProvider`（テレメトリ） | 中 |
| ★★ | 4. 評価 | 任意 | 🔜 `LocalEvaluator` | 中 |
| ★ | 5. 履歴圧縮 | 任意・実験的 API | 🔜 `CompactionProvider` | 中 |
| ★ | 6. Aspire AppHost | 任意 | —（インフラ） | 小〜中 |

---

### 📡 2. OTLP コレクタ受信確認 ★★（動作確認・低工数）

- **優先度の理由**: 低工数で **P9（可観測性）を締める**動作確認。コード変更はほぼ不要。
- **背景**: P9 で初期化・送信試行・Exit 0 までは確認済み。**ダッシュボードでの可視受信は未実施**（コレクタ起動が必要）。
- **作業内容 / 確認方法**:
  - Aspire Dashboard / OTel Collector を起動（[`README.md`](../README.md) 手順）→ `Telemetry:Otlp:Enabled=true` で `diagnose` / 対話を実行 → `PcAgent.Diagnostics`（`diagnostics.snapshot`）/ `PcAgent.Agent` のスパンがダッシュボードに出ることを確認。

### 📈 3. メトリクス（MeterProvider + カスタムメトリクス）★★（任意）

- **🔜 使用予定の AF 機能**: テレメトリのメトリクス面（`MeterProvider`）。トレースは実装済み・メトリクスは未実装。
- **優先度の理由**: トレースは実装済みのため**付加価値**。運用で数値を見たい場合に効く。
- **作業内容**:
  1. `Meter`（"PcAgent"）を用意し計器を定義: 収集件数（Counter）、診断重大度別件数（Counter）、処理時間（Histogram）。
  2. `SnapshotBuilder` / `RuleEngine` / コマンドフィルタ（`ExecutionTimeFilter`）で記録。
  3. `AgentTelemetry` に `Sdk.CreateMeterProviderBuilder().AddMeter("PcAgent").AddOtlpExporter(...)` を `Telemetry:Otlp:Enabled` 連動で追加。
- **確認方法**:
  - ビルド 0/0。
  - OTLP 有効 + `diagnose` 実行 → コレクタ/ダッシュボードでカウンタ・ヒストグラムが確認できる。

### 🧪 4. 評価（LocalEvaluator）★★（任意）

- **🔜 使用予定の AF 機能**: **評価（ローカル検査）** `LocalEvaluator` / `EvalChecks`（追加 NuGet 不要）。
- **優先度の理由**: 製品化・回帰防止（CI）の観点で有用。個人ツール用途なら後回し可。
- **作業内容**:
  1. 評価用プロジェクト/テストを追加し、`LocalEvaluator` で代表プロンプト群の応答を採点。
  2. 判定基準（ツール呼び出しの有無、出典提示、数値の妥当性 等）を定義。
  3. CI に組み込む。
- **確認方法**:
  - 評価実行 → スコア/レポート出力。閾値で pass/fail を判定。

### 🗜️ 5. 履歴圧縮（CompactionProvider）★（任意・実験的 API）

- **🔜 使用予定の AF 機能**: **履歴の圧縮** `CompactionProvider`（**実験的 `MAAI001`**）。
- **優先度の理由**: 短い診断対話では必要性が低い。実験的 API を伴うため優先度低。
- **作業内容**:
  1. `CompactionProvider`（または履歴 reducer）を agent に設定し、履歴増加時に古いターンを要約（決定的間引き / LLM 要約のいずれか）。
  2. `MAAI001` の抑制が必要なら **事前相談**（AGENTS.md）。
- **確認方法**:
  - 長い多ターン対話 → 履歴が圧縮されコンテキスト超過せず継続（古いターンが要約される）。

### 🧭 6. Aspire AppHost ★（任意）

- **優先度の理由**: PcAgent は**対話 TUI** のため AppHost からの常駐起動は不自然。**スタンドアロンのダッシュボードで代替可能**なため価値は低い。
- **背景**: OTLP の受信・可視化用。アプリ本体は **Aspire 非依存**（`ServiceDefaults` は作らない方針）。
- **作業内容**:
  1. 最小構成: **スタンドアロン Aspire Dashboard コンテナ**で受信（[`README.md`](../README.md) に手順あり）。
  2. AppHost を作る場合: Aspire AppHost プロジェクトを追加しダッシュボードを起動（実質は OTLP シンク用途）。
- **確認方法**:
  - ダッシュボード起動 → OTLP 有効の PcAgent からトレース（/メトリクス）が到達する。

---

## 📎 横断メモ

- **各変更の終わり**: 警告ゼロビルド（`AnalysisMode=All`）、`.cs`/`.csproj`/`.json`/`.md` は UTF-8 BOMなし + CRLF、警告抑制が要る場合は事前相談（AGENTS.md）。
- **Git**: コミットはユーザーが実施する。
