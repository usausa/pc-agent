// OTLP 受信用の Aspire ダッシュボード(簡易 OTEL サービス)。特定アプリに依存しない。
// 単体で起動しておき、PcAgent を別途 OTLP 有効で実行すると、トレースがここに届く。
// 受信エンドポイント(OTLP)とダッシュボード URL は起動ログに表示される。
var builder = DistributedApplication.CreateBuilder(args);

builder.Build().Run();
