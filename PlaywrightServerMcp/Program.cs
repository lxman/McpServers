using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using Playwright.Core.Services;
using PlaywrightServerMcp;
using PlaywrightServerMcp.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost.
    // The old "logs/playwright-server-mcp-.log" resolved against the working directory, which is a
    // versioned deploy directory now.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "playwright");

    Log.Information("Starting Playwright server");

    // Screenshots, PDFs and downloads were written under the working directory, which the gateway
    // makes a versioned deploy path -- so each deploy would orphan the artefacts of the last.
    // Deliberately does NOT touch the Angular tools' workingDirectory defaults: those mean the
    // user's project, not our output.
    OutputPaths.Root = McpHttpHost.DataPathFor("playwright");
    Log.Information("Output directory: {OutputRoot}", OutputPaths.Root);

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

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithToolsFromAssembly();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Playwright Server stopped unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
