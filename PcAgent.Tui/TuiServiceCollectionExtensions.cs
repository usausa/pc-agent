namespace PcAgent.Tui;

using Microsoft.Extensions.DependencyInjection;

using PcAgent.Tui.Repl;

// TUI(REPL・スラッシュコマンド・ディスパッチ)の DI 登録拡張。
public static class TuiServiceCollectionExtensions
{
    public static IServiceCollection AddTui(this IServiceCollection services)
    {
        services.AddSingleton<ISlashCommand, HelpCommand>();
        services.AddSingleton<ISlashCommand, ClearCommand>();
        services.AddSingleton<ISlashCommand, ExitCommand>();
        services.AddSingleton<ISlashCommand, ModelCommand>();
        services.AddSingleton<ISlashCommand, InfoSlashCommand>();
        services.AddSingleton<ISlashCommand, StatusCommand>();
        services.AddSingleton<ISlashCommand, ConfigCommand>();
        services.AddSingleton<ISlashCommand, DoctorCommand>();
        services.AddSingleton<ISlashCommand, HealthCommand>();
        services.AddSingleton<ISlashCommand, DiagnoseSlashCommand>();
        services.AddSingleton<ISlashCommand, ReportCommand>();
        services.AddSingleton<ISlashCommand, RulesCommand>();

        services.AddSingleton<ShellRunner>();
        services.AddSingleton<InputDispatcher>();
        services.AddSingleton<ReplSession>();

        return services;
    }
}
