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

// Configure Serilog to write to a file (stdout is reserved for MCP protocol)
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "seleniummcp.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // MongoDB configuration
    var mongoSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
    if (mongoSettings != null && !string.IsNullOrEmpty(mongoSettings.ConnectionString))
    {
        builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
        builder.Services.AddScoped(provider =>
            provider.GetRequiredService<IMongoClient>().GetDatabase(mongoSettings.DatabaseName));
    }

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

    var host = builder.Build();

    Log.Information("SeleniumMcp starting...");
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
