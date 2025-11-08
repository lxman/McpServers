using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoMcp.McpTools;
using MongoServer.Core;
using MongoServer.Core.Services;
using Serilog;

// Configure Serilog to write to a file (stdout is reserved for MCP protocol)
string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "mongomcp.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Register MongoServer.Core services
    builder.Services.AddSingleton<MongoDbService>();
    builder.Services.AddSingleton<ConnectionManager>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<ConnectionManager>>();
        return new ConnectionManager(logger);
    });
    builder.Services.AddSingleton<CrossServerOperations>(sp =>
    {
        var mongoService = sp.GetRequiredService<MongoDbService>();
        var logger = sp.GetRequiredService<ILogger<CrossServerOperations>>();
        return new CrossServerOperations(mongoService.ConnectionManager, logger);
    });

    // Configure MCP Server with all tool classes
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<ConnectionTools>()
        .WithTools<DatabaseTools>()
        .WithTools<CollectionTools>()
        .WithTools<AdvancedTools>()
        .WithTools<CrossServerTools>();

    IHost host = builder.Build();

    Log.Information("MongoMcp starting...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MongoMcp terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
