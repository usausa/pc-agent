namespace PcAgent.Agent;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PcAgent.Agent.Options;
using PcAgent.Agent.Tools;

// エージェント機能の DI 登録拡張。
public static class AgentServiceCollectionExtensions
{
    // エージェントに関するオプションとサービスを登録する。
    public static IServiceCollection AddPcAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        services.AddSingleton<PcInfoTools>();
        services.AddSingleton<IAgentConversation, PcAgentConversation>();

        return services;
    }
}
