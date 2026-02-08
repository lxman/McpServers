using System.Reflection;
using Edgar.Core.Services;
using EdgarMcp.McpTools;
using Mcp.ResponseGuard.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SerilogFileWriter;

// Set the base path to where the DLL lives so appsettings.json is found
string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
string executableDir = Path.GetDirectoryName(executablePath) ?? Directory.GetCurrentDirectory();

// Setup MCP-safe logging (redirects Console to file)
string logPath = Path.Combine(executableDir, "logs", "edgar-mcp-.log");
Log.Logger = McpLoggingExtensions.SetupMcpLogging(logPath);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = executableDir
});

// Load user secrets (API keys, etc.)
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

// Configure Serilog
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

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

// Add MCP server with all tool types
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FilingTools>()
    .WithTools<HoldingsTools>()
    .WithTools<TradeTools>();

IHost app = builder.Build();

await app.RunAsync();
