namespace PcAgent.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PcAgent.Diagnostics.Collectors;
using PcAgent.Diagnostics.Hardware;
using PcAgent.Diagnostics.Options;
using PcAgent.Diagnostics.Rules;

// 情報取得・診断機能の DI 登録拡張。
public static class DiagnosticsServiceCollectionExtensions
{
    // 情報取得・診断に関するオプションとサービスを登録する。
    public static IServiceCollection AddDiagnostics(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CollectionOptions>(configuration.GetSection(CollectionOptions.SectionName));
        services.Configure<DiagnosticsOptions>(configuration.GetSection(DiagnosticsOptions.SectionName));
        services.Configure<ActionsOptions>(configuration.GetSection(ActionsOptions.SectionName));

        services.AddSingleton<HardwareMonitorSource>();

        services.AddSingleton<ICollector, CpuCollector>();
        services.AddSingleton<ICollector, GpuCollector>();
        services.AddSingleton<ICollector, MemoryCollector>();
        services.AddSingleton<ICollector, MotherboardCollector>();
        services.AddSingleton<ICollector, DiskCollector>();
        services.AddSingleton<ICollector, NetworkCollector>();
        services.AddSingleton<ICollector, BatteryCollector>();
        services.AddSingleton<ICollector, SmartCollector>();
        services.AddSingleton<ICollector, SystemCollector>();

        services.AddSingleton<SnapshotBuilder>();
        services.AddSingleton<RuleEngine>();

        return services;
    }
}
