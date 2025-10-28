using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SqlMcp.Models;
using SqlMcp.Services;
using SqlMcp.Services.Interfaces;
using SqlMcp.Tools;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

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