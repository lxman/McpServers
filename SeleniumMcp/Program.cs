using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services;
using SeleniumChrome.Core.Services.Enhanced;
using SeleniumChrome.Core.Services.Scrapers;
using SeleniumMcp.McpTools;
using Serilog;
using SerilogFileWriter;

// Configure Serilog to write to a file (stdout is reserved for MCP protocol)
string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "selenium-mcp-.log");
Log.Logger = McpLoggingExtensions.SetupMcpLogging(logPath);

try
{
    Log.Information("Starting SeleniumMcp");
    
    // Configure builder to use the executable's directory as the content root
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: false);

    Log.Logger.Debug($"AppContext.BaseDirectory is {AppContext.BaseDirectory}");
    Log.Logger.Debug($"Content root is {builder.Environment.ContentRootPath}");

    // MongoDB configuration - required for most services
    MongoDbSettings mongoSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()
                                    ?? throw new InvalidOperationException("MongoDbSettings configuration is required");

    Log.Logger.Debug($"MongoDbSettings is {mongoSettings}");

    if (string.IsNullOrEmpty(mongoSettings.ConnectionString))
        throw new InvalidOperationException("MongoDB ConnectionString is required in configuration");

    builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
    builder.Services.AddScoped<IMongoDatabase>(provider =>
        provider.GetRequiredService<IMongoClient>().GetDatabase(mongoSettings.DatabaseName));

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

    // Configure MCP Server with all tool classes
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<JobScrapingTools>()
        .WithTools<JobStorageTools>()
        .WithTools<EmailAlertTools>()
        .WithTools<AnalysisTools>()
        .WithTools<ApplicationTrackingTools>()
        .WithTools<ConfigurationTools>();

    IHost host = builder.Build();

    await host.RunAsync();
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
