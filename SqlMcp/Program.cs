using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mcp.Database.Core.Sql;
using Mcp.ResponseGuard.Configuration;
using Mcp.ResponseGuard.Services;
using ModelContextProtocol.AspNetCore;
using Serilog;
using SqlMcp.Tools;
using SqlServer.Core.Models;
using SqlServer.Core.Services;
using SqlServer.Core.Services.Interfaces;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // It also sets ContentRootPath to AppContext.BaseDirectory, which is what the executableDir
    // dance here used to be for -- appsettings.json is found beside the assembly rather than beside
    // whatever the working directory happens to be.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "sql-database-explorer");

    string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    // Diagnostics kept: they exist to explain a server that starts with zero configured
    // connections, and that failure mode is unchanged by the transport.
    Log.Information("=== SqlMcp Startup Diagnostics ===");
    Log.Information("Content root: {ContentRoot}", AppContext.BaseDirectory);
    Log.Information("Looking for appsettings.json at: {AppSettingsPath}", appSettingsPath);
    Log.Information("appsettings.json exists: {Exists}", File.Exists(appSettingsPath));
    Log.Information("Log file location: {LogPath}", McpHttpHost.LogPathFor("sql-database-explorer"));

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

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<SqlConnectionTools>()
        .WithTools<SqlQueryTools>()
        .WithTools<SqlSchemaTools>()
        .WithTools<SqlTransactionTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    // Log startup information
    var connectionManager = app.Services.GetRequiredService<SqlConnectionManager>();
    List<string> availableConnections = connectionManager.GetConnectionNames();
    Log.Information("SqlMcp starting with {ConnectionCount} configured connections: {Connections}",
        availableConnections.Count,
        string.Join(", ", availableConnections));
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SqlMcp terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}