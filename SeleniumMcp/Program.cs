using Mcp.Database.Core.MongoDB;
using Mcp.Hosting.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services;
using SeleniumChrome.Core.Services.Enhanced;
using SeleniumChrome.Core.Services.Scrapers;
using SeleniumMcp.McpTools;
using Serilog;

try
{
    // Logging, the loopback listener and the gateway's port-file contract all live in McpHttpHost,
    // which also sets ContentRootPath to AppContext.BaseDirectory -- what the explicit setting here
    // was for. The old log path was built from AppContext.BaseDirectory too, which is a versioned
    // deploy directory now, so logs would have scattered one directory per deploy.
    WebApplicationBuilder builder = McpHttpHost.CreateBuilder(args, "selenium");

    Log.Information("Starting SeleniumMcp");

    // Screenshots used a bare relative "Screenshots" directory, so they landed wherever the process
    // happened to be running -- a versioned deploy directory under the gateway, orphaned at the
    // next deploy. Anchor them beside every other server's data.
    ScreenshotStore.Root = Path.Combine(McpHttpHost.DataPathFor("selenium"), "Screenshots");
    Log.Information("Screenshot directory: {ScreenshotRoot}", ScreenshotStore.Root);

    // MongoDB configuration - required for most services
    MongoDbSettings mongoSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()
                                    ?? throw new InvalidOperationException("MongoDbSettings configuration is required");

    Log.Logger.Debug($"MongoDbSettings is {mongoSettings}");

    if (string.IsNullOrEmpty(mongoSettings.ConnectionString))
        throw new InvalidOperationException("MongoDB ConnectionString is required in configuration");

    // Register MongoDB connection manager
    builder.Services.AddMongoConnectionManager();

    // HttpClient for web requests
    builder.Services.AddHttpClient();

    // Core services
    builder.Services.AddScoped<IEnhancedJobScrapingService, EnhancedJobScrapingService>();
    builder.Services.AddScoped<IJobSiteScraperFactory, JobSiteScraperFactory>();

    // Individual scrapers (implement IJobSiteScraper via BaseJobScraper)
    builder.Services.AddScoped<DiceScraper>();
    builder.Services.AddScoped<BuiltInScraper>();
    builder.Services.AddScoped<AngelListScraper>();
    builder.Services.AddScoped<StackOverflowScraper>();
    builder.Services.AddScoped<HubSpotScraper>();
    builder.Services.AddScoped<SimplifyJobsScraper>();
    builder.Services.AddScoped<GoogleSimplifyJobsService>();
    builder.Services.AddScoped<SimplifyJobsApiService>();

    // Email alerts
    builder.Services.AddScoped<EmailJobAlertService>();

    // Phase 1 Enhanced Services
    builder.Services.AddScoped<NetDeveloperJobScorer>();
    builder.Services.AddScoped<IntelligentBulkProcessor>();
    builder.Services.AddScoped<AutomatedSimplifySearch>();

    // Phase 2 Enhanced Services
    builder.Services.AddScoped<SmartDeduplicationService>();
    builder.Services.AddScoped<ApplicationManagementService>();
    builder.Services.AddScoped<MarketIntelligenceService>();
    builder.Services.AddSingleton<JobQueueManager>();

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
        .WithTools<JobScrapingTools>()
        .WithTools<JobStorageTools>()
        .WithTools<EmailAlertTools>()
        .WithTools<AnalysisTools>()
        .WithTools<ApplicationTrackingTools>()
        .WithTools<ConfigurationTools>();

    WebApplication app = builder.Build();
    app.MapMcpHost();

    // Initialize MongoDB connection
    var connectionManager = app.Services.GetRequiredService<MongoConnectionManager>();
    await connectionManager.AddConnectionAsync("default", mongoSettings.ConnectionString, mongoSettings.DatabaseName);
    connectionManager.SetDefaultConnection("default");

    Log.Information("SeleniumMcp starting with MongoDB connection to database: {DatabaseName}", mongoSettings.DatabaseName);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SeleniumMcp terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
