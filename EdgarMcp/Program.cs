using System.Reflection;
using Edgar.Core.Services;
using EdgarMcp.McpTools;
using Mcp.Hosting.Core;
using Mcp.ResponseGuard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // It also sets ContentRootPath to AppContext.BaseDirectory, which is what the hand-rolled
    // executableDir dance here used to be for -- appsettings.json is found beside the assembly
    // rather than beside whatever the working directory happens to be.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "edgar");

    Log.Information("Starting Edgar MCP server");

    // Load user secrets (API keys, etc.). Still explicit: WebApplication.CreateBuilder adds them
    // only in the Development environment, and a gateway-launched backend is not in it.
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

    // Edgar:DataDirectory ships as "./data", which resolved against the working directory. That
    // directory is now a VERSIONED deploy path, so the holdings archive and the CUSIP cache would
    // move to a new empty location on every deploy and silently orphan everything written before
    // it. Anchoring the value here fixes both readers -- HoldingsStore and CusipTickerMapper --
    // because they read this one key. An absolute value configured by the user still wins.
    string dataDirectory = McpHttpHost.ResolveDataDirectory(
        builder.Configuration["Edgar:DataDirectory"], "edgar");

    builder.Configuration.AddInMemoryCollection(
        new Dictionary<string, string?> { ["Edgar:DataDirectory"] = dataDirectory });

    Log.Information("Edgar data directory: {DataDirectory}", dataDirectory);

    // Register HttpClientFactory
    builder.Services.AddHttpClient();

    // Register Edgar.Core services
    builder.Services.AddSingleton<EdgarApiClient>();
    builder.Services.AddSingleton<Filing13FParser>();
    builder.Services.AddSingleton<HoldingsDiffer>();
    builder.Services.AddSingleton<CusipTickerMapper>();
    builder.Services.AddSingleton<PortfolioScaler>();
    builder.Services.AddSingleton<TradeExecutor>();
    builder.Services.AddSingleton<HoldingsStore>();

    // Register response guard
    builder.Services.AddSingleton<OutputGuard>();

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<FilingTools>()
        .WithTools<HoldingsTools>()
        .WithTools<TradeTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Edgar MCP server terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
