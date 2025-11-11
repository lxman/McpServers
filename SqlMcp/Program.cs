using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mcp.Database.Core.Sql;
using Mcp.ResponseGuard.Configuration;
using Mcp.ResponseGuard.Services;
using Serilog;
using SerilogFileWriter;
using SqlMcp.Tools;
using SqlServer.Core.Models;
using SqlServer.Core.Services;
using SqlServer.Core.Services.Interfaces;

// Configure Serilog for file logging (STDIO servers cannot log to console)
string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "sql-mcp-.log");
Log.Logger = McpLoggingExtensions.SetupMcpLogging(logPath);

try
{
    // Set the base path to the directory where the DLL is located
    // This ensures appsettings.json is found even when the working directory is different
    string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
    string executableDir = Path.GetDirectoryName(executablePath) ?? Directory.GetCurrentDirectory();

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = executableDir
    });

    // Log diagnostic information about working directory and configuration
    string currentDir = Directory.GetCurrentDirectory();
    string appSettingsPath = Path.Combine(executableDir, "appsettings.json");
    bool appSettingsExists = File.Exists(appSettingsPath);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    // Log diagnostic information to help troubleshoot configuration issues
    Log.Information("=== SqlMcp Startup Diagnostics ===");
    Log.Information("Current Working Directory: {CurrentDir}", currentDir);
    Log.Information("Executable Directory (ContentRootPath): {ExecDir}", executableDir);

    Log.Information("Looking for appsettings.json at: {AppSettingsPath}", appSettingsPath);
    Log.Information("appsettings.json exists: {Exists}", appSettingsExists);
    Log.Information("Log file location: {LogPath}", logPath);

    // Log configuration source paths
    List<string> configSources = builder.Configuration.Sources
        .Select(s => s.GetType().Name)
        .ToList();
    Log.Information("Configuration sources: {Sources}", string.Join(", ", configSources));


    // Configure SqlMcp settings
    builder.Services.Configure<SqlConfiguration>(
        builder.Configuration.GetSection("SqlConfiguration"));

    // Register SQL connection manager
    builder.Services.AddSqlConnectionManager();

    // Register services
    builder.Services.AddSingleton<IQueryExecutor, QueryExecutor>();
    builder.Services.AddSingleton<ISchemaInspector, SchemaInspector>();
    builder.Services.AddSingleton<ITransactionManager, TransactionManager>();
    builder.Services.AddSingleton<IAuditLogger, AuditLogger>();

    // Register OutputGuard with custom 15k token limit for SQL query operations
    builder.Services.AddSingleton(sp => new OutputGuard(
        sp.GetRequiredService<ILogger<OutputGuard>>(),
        new OutputGuardOptions { SafeTokenLimit = 15_000 }));

    // Configure MCP server
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<SqlConnectionTools>()
        .WithTools<SqlQueryTools>()
        .WithTools<SqlSchemaTools>()
        .WithTools<SqlTransactionTools>();

    IHost host = builder.Build();

    // Log startup information
    var connectionManager = host.Services.GetRequiredService<SqlConnectionManager>();
    List<string> availableConnections = connectionManager.GetConnectionNames();
    Log.Information("SqlMcp starting with {ConnectionCount} configured connections: {Connections}",
        availableConnections.Count,
        string.Join(", ", availableConnections));
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SqlMcp terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}