using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mcp.Database.Core.Redis;
using RedisBrowser.Core.Services;
using RedisMcp.McpTools;
using Serilog;
using SerilogFileWriter;

// Configure Serilog to write to a file (stdout is reserved for MCP protocol)
string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "redis-mcp-.log");
Log.Logger = McpLoggingExtensions.SetupMcpLogging(logPath);

try
{
    Log.Information("Starting RedisMcp");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    // Register Redis connection manager
    builder.Services.AddRedisConnectionManager();

    // Register RedisBrowser.Core services
    builder.Services.AddSingleton<RedisService>();

    // Configure MCP Server with all tool classes
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<ConnectionTools>()
        .WithTools<KeyTools>()
        .WithTools<ExpiryTools>()
        .WithTools<ServerTools>();

    IHost host = builder.Build();

    Log.Information("RedisMcp starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RedisMcp terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
