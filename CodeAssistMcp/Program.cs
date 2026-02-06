using CodeAssist.Core.Extensions;
using CodeAssistMcp.McpTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

// Configure Serilog to write to a file (not stdout - MCP uses stdio)
Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/codeassist-mcp-.log");

try
{
    Log.Information("Starting CodeAssist MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    // Register CodeAssist services
    builder.Services.AddCodeAssistServices(builder.Configuration);

    // Configure MCP server with STDIO transport
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<HealthTools>()
        .WithTools<IndexTools>()
        .WithTools<SearchTools>()
        .WithTools<RepositoryTools>();

    IHost host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CodeAssist MCP server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
