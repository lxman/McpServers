using AwsMcp.McpTools;
using AwsServer.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// Configure Serilog to write to a file (not stdout!)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/aws-mcp-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AWS MCP server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    // Configure logging
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();

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
    Log.CloseAndFlush();
}