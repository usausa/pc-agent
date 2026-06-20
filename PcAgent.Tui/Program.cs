using Microsoft.Extensions.DependencyInjection;

using PcAgent.Agent;
using PcAgent.Diagnostics;
using PcAgent.Tui.Commands;
using PcAgent.Tui.Filters;
using PcAgent.Tui.Options;

using Smart.CommandLine.Hosting;

var builder = CommandHost.CreateBuilder(args).UseDefaults();

builder.Services.AddDiagnostics(builder.Configuration);
builder.Services.AddPcAgent(builder.Configuration);
builder.Services.Configure<UiOptions>(builder.Configuration.GetSection(UiOptions.SectionName));

builder.ConfigureCommands(static commands =>
{
    commands.AddGlobalFilter<ExecutionTimeFilter>(order: -100);
    commands.AddGlobalFilter<CancellationFilter>(order: int.MaxValue);

    commands.ConfigureRootCommand(static root => root
        .WithDescription("PC diagnostics and information agent")
        .UseHandler<RootCommandHandler>());

    commands.AddCommand<InfoCommand>();
    commands.AddCommand<DiagnoseCommand>();
});

var host = builder.Build();
return await host.RunAsync();
