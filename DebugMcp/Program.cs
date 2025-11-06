using DebugMcp.McpTools;
using DebugServer.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// Configure Serilog to write to a file
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/debugmcp-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting DebugMcp server");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    // Configure logging to use Serilog
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);

    // Register debugging services
    // Note: MiClient is IDisposable and will be disposed by the DI container on shutdown
    builder.Services.AddSingleton<MiClient>();
    builder.Services.AddSingleton<DebuggerSessionManager>();

    // Register MCP tools
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<SessionManagementTools>()
        .WithTools<BreakpointTools>()
        .WithTools<ExecutionTools>()
        .WithTools<InspectionTools>();

    IHost host = builder.Build();

    // Register shutdown handler to ensure clean disconnect of debug sessions
    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        Log.Information("Application stopping - cleaning up debug sessions");
        try
        {
            var miClient = host.Services.GetRequiredService<MiClient>();
            // MiClient.Dispose() will be called automatically by DI container
            // which will clean up all active sessions
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during shutdown cleanup");
        }
    });

    Log.Information("DebugMcp server started successfully");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DebugMcp server terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("DebugMcp server shutting down");
    Log.CloseAndFlush();
}