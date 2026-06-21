using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using PcAgent.Agent;
using PcAgent.Diagnostics;
using PcAgent.Tui;
using PcAgent.Tui.Commands;
using PcAgent.Tui.Filters;
using PcAgent.Tui.Options;

using Smart.CommandLine.Hosting;

var builder = CommandHost.CreateBuilder(args).UseDefaults();

// API キー等は user-secrets / 環境変数からも読み込む(環境変数を優先)。
var entryAssembly = Assembly.GetEntryAssembly();
if (entryAssembly is not null)
{
    builder.Configuration.AddUserSecrets(entryAssembly, optional: true);
}

builder.Configuration.AddEnvironmentVariables();

// ログは標準エラーへ。標準出力はコマンドの出力(JSON 等)専用にする。
builder.Services.Configure<ConsoleLoggerOptions>(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddDiagnostics(builder.Configuration);
builder.Services.AddPcAgent(builder.Configuration);
builder.Services.AddTui();
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
