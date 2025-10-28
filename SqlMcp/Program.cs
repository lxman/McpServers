using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SqlMcp.Models;
using SqlMcp.Services;
using SqlMcp.Services.Interfaces;
using SqlMcp.Tools;

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



// Configure Serilog for file logging (STDIO servers cannot log to console)
string logPath = Path.Combine(Path.GetTempPath(), "SqlMcp", "logs", "sqlmcp-.log");
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// Log diagnostic information to help troubleshoot configuration issues
Log.Information("=== SqlMcp Startup Diagnostics ===");
Log.Information("Current Working Directory: {CurrentDir}", currentDir);
Log.Information("Executable Directory (ContentRootPath): {ExecDir}", executableDir);

Log.Information("Looking for appsettings.json at: {AppSettingsPath}", appSettingsPath);
Log.Information("appsettings.json exists: {Exists}", appSettingsExists);
Log.Information("Log file location: {LogPath}", logPath);

// Log configuration source paths
var configSources = builder.Configuration.Sources
    .Select(s => s.GetType().Name)
    .ToList();
Log.Information("Configuration sources: {Sources}", string.Join(", ", configSources));


// Configure SqlMcp settings
builder.Services.Configure<SqlConfiguration>(
    builder.Configuration.GetSection("SqlConfiguration"));

// Register services
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
builder.Services.AddSingleton<IQueryExecutor, QueryExecutor>();
builder.Services.AddSingleton<ISchemaInspector, SchemaInspector>();
builder.Services.AddSingleton<ITransactionManager, TransactionManager>();
builder.Services.AddSingleton<IAuditLogger, AuditLogger>();

// Configure MCP server
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SqlConnectionTools>()
    .WithTools<SqlQueryTools>()
    .WithTools<SqlSchemaTools>()
    .WithTools<SqlTransactionTools>();

IHost host = builder.Build();

// Log startup information
var connectionManager = host.Services.GetRequiredService<IConnectionManager>();
List<string> availableConnections = connectionManager.GetAvailableConnections().ToList();
Log.Information("SqlMcp starting with {ConnectionCount} configured connections: {Connections}",
    availableConnections.Count,
    string.Join(", ", availableConnections));

try
{
    await host.RunAsync();
}
finally
{
    Log.Information("SqlMcp shutting down");
    await Log.CloseAndFlushAsync();
}