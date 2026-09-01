using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mcp.Database.Core.Sql;
using Microsoft.Data.Sqlite;
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

    // SQLite data sources are anchored here, in the composition root, for the same reason edgar's
    // data directory was: a relative "Data Source=./local.db" used to resolve against whatever
    // working directory the client handed us, and now resolves against a VERSIONED deploy
    // directory -- so the database would be created under v-A and silently be a different, empty
    // file under v-B. Absolute paths are left alone as the user's escape hatch.
    builder.Services.PostConfigure<SqlConfiguration>(sqlConfig =>
    {
        string dataRoot = McpHttpHost.ResolveDataDirectory(null, "sql-database-explorer");

        foreach (ConnectionConfig conn in sqlConfig.Connections.Values)
        {
            if (!string.Equals(conn.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase)) continue;

            var csb = new SqliteConnectionStringBuilder(conn.ConnectionString);

            // An in-memory database has no file to anchor, and neither does an empty data source.
            if (string.IsNullOrWhiteSpace(csb.DataSource)) continue;
            if (csb.DataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase)) continue;

            csb.DataSource = McpHttpHost.ResolveDataDirectory(csb.DataSource, "sql-database-explorer");
            conn.ConnectionString = csb.ConnectionString;
        }

        // SQLite creates the database file but not the directory above it.
        Directory.CreateDirectory(dataRoot);
    });

    // Register SQL connection manager
    builder.Services.AddSqlConnectionManager();

    // Opens the connections named in SqlConfiguration on first use. Without it nothing ever
    // populates SqlConnectionManager -- there is no connect tool -- so every connection name,
    // including the ones in appsettings.json, answered "not found. Please connect first."
    builder.Services.AddSingleton<ConnectionResolver>();

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