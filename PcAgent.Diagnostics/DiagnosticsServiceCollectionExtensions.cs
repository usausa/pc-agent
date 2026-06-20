namespace PcAgent.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PcAgent.Diagnostics.Options;

// 情報取得・診断機能の DI 登録拡張。
public static class DiagnosticsServiceCollectionExtensions
{
    // 情報取得・診断に関するオプションとサービスを登録する。
    public static IServiceCollection AddDiagnostics(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CollectionOptions>(configuration.GetSection(CollectionOptions.SectionName));
        services.Configure<DiagnosticsOptions>(configuration.GetSection(DiagnosticsOptions.SectionName));
        services.Configure<ActionsOptions>(configuration.GetSection(ActionsOptions.SectionName));

        // 収集(ICollector)とルールエンジンはフェーズ2・6で登録する。
        return services;
    }
}
