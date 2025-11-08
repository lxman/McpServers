using AwsMcp.McpTools;
using AwsServer.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

// Configure Serilog to write to a file (not stdout!)
Log.Logger = McpLoggingExtensions.SetupMcpLogging("logs/aws-mcp-.log");

try
{
    Log.Information("Starting AWS MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    // Register AWS Core services
    builder.Services.AddAwsServices();

    // Configure MCP server with STDIO transport
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        // AWS Service Tools
        .WithTools<S3Tools>()
        .WithTools<CloudWatchTools>()
        .WithTools<EcsTools>()
        .WithTools<EcrTools>()
        .WithTools<QuickSightTools>();

    IHost host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AWS MCP server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}