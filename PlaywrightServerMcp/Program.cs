using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Playwright.Core.Services;
using PlaywrightServerMcp.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;

// Suppress console output for STDIO protocol
Console.SetOut(TextWriter.Null);
Console.SetError(TextWriter.Null);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure JSON serialization options globally to handle deep object structures
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.MaxDepth = 512; // Increased from default 64 to handle deep Angular component trees
    options.ReferenceHandler = ReferenceHandler.IgnoreCycles; // Handle circular references
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.WriteIndented = true;
});

// Register core services from Playwright.Core
builder.Services
    .AddSingleton<ToolService>()
    .AddSingleton<PlaywrightSessionManager>()
    .AddSingleton<ChromeService>()
    .AddSingleton<FirefoxService>()
    .AddSingleton<WebKitService>();

// Register all tool classes
builder.Services
    .AddSingleton<PlaywrightTools>()
    .AddSingleton<BrowserManagementTools>()
    .AddSingleton<InteractionTestingTools>()
    .AddSingleton<VisualTestingTools>()
    .AddSingleton<AccessibilityTestingTools>()
    .AddSingleton<AdvancedTestingTools>()
    .AddSingleton<NetworkTestingTools>()
    .AddSingleton<PerformanceTestingTools>()
    .AddSingleton<DatabaseTestingTools>()
    .AddSingleton<TaderatcsTestingTools>();

// Register Angular-specific tool classes
builder.Services
    .AddSingleton<AngularStyleTools>()
    .AddSingleton<AngularBundleAnalyzer>()
    .AddSingleton<AngularChangeDetectionAnalyzer>()
    .AddSingleton<AngularCircularDependencyDetector>()
    .AddSingleton<AngularCliIntegration>()
    .AddSingleton<AngularComponentAnalyzer>()
    .AddSingleton<AngularComponentContractTesting>()
    .AddSingleton<AngularConfigurationAnalyzer>()
    .AddSingleton<AngularLifecycleMonitor>()
    .AddSingleton<AngularMaterialAccessibilityTesting>()
    .AddSingleton<AngularNgrxTesting>()
    .AddSingleton<AngularPerformanceTools>()
    .AddSingleton<AngularRoutingTesting>()
    .AddSingleton<AngularServiceDependencyAnalyzer>()
    .AddSingleton<AngularSignalMonitor>()
    .AddSingleton<AngularStabilityDetection>()
    .AddSingleton<AngularStyleGuideCompliance>()
    .AddSingleton<AngularTestingIntegration>()
    .AddSingleton<AngularZonelessTesting>();

// Configure logging to suppress noisy output
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.SetMinimumLevel(LogLevel.Error);

    // Suppress noisy framework logs
    logging.AddFilter("Microsoft", LogLevel.None);
    logging.AddFilter("System", LogLevel.None);
    logging.AddFilter("Microsoft.Playwright", LogLevel.None);
});

// Configure the MCP server with STDIO transport
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

IHost host = builder.Build();
await host.RunAsync();
