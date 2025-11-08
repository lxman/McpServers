using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedisBrowser.Core.Services;
using RedisMcp.McpTools;
using Serilog;

// Configure Serilog to write to a file (stdout is reserved for MCP protocol)
string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "redismcp.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
    Console.SetOut(TextWriter.Null);
    Console.SetError(TextWriter.Null);

    // Add Serilog
    builder.Services.AddSerilog();

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
