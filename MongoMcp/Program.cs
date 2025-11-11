using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoMcp.McpTools;
using MongoServer.Core;
using MongoServer.Core.Services;
using Serilog;
using SerilogFileWriter;

// Configure Serilog to write to a file (stdout is reserved for MCP protocol)
string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "mongo-mcp-.log");
Log.Logger = McpLoggingExtensions.SetupMcpLogging(logPath);

try
{
    // Create builder WITHOUT default logging to prevent console output
    var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        DisableDefaults = true  // This prevents default console logging
    });
    
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    // Register MongoServer.Core services
    builder.Services.AddSingleton<MongoDbService>();
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