using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using SeleniumChromeTool.Models;
using SeleniumChromeTool.Services;
using SeleniumChromeTool.Services.Enhanced;
using SeleniumChromeTool.Services.Scrapers;
using Serilog;
using Serilog.Events;

namespace SeleniumChromeTool;

public class Program
{
    public static void Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Starting Job Scraping Service");

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Use Serilog for logging
            builder.Host.UseSerilog();

            // Add services to the container
            builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo 
            { 
                Title = "Job Scraping API", 
                Version = "v1.0",
                Description = "Job scraping service API"
            });
        });

        // Configure MongoDB
        builder.Services.Configure<MongoDbSettings>(options =>
        {
            options.ConnectionString = "mongodb://localhost:27017";
            options.DatabaseName = "claudeDatabase";
        });

        // Register MongoDB client and database
        builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            MongoDbSettings settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            MongoDbSettings settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return client.GetDatabase(settings.DatabaseName);
        });

        // Register job scraping services
        builder.Services.AddScoped<IEnhancedJobScrapingService, EnhancedJobScrapingService>();
        builder.Services.AddScoped<IJobSiteScraperFactory, JobSiteScraperFactory>();
        builder.Services.AddScoped<EmailJobAlertService>();

        // Register individual scrapers
        builder.Services.AddScoped<DiceScraper>();
        builder.Services.AddScoped<BuiltInScraper>();
        builder.Services.AddScoped<AngelListScraper>();
        builder.Services.AddScoped<StackOverflowScraper>();
        builder.Services.AddScoped<HubSpotScraper>();
        builder.Services.AddScoped<SimplifyJobsScraper>();
        
        // Register Streamlined SimplifyJobs API Service (Final Solution)
        builder.Services.AddScoped<SimplifyJobsApiService>();
        
        // Register Phase 2 Google Integration Service
        builder.Services.AddScoped<GoogleSimplifyJobsService>();
        
        // Register HttpClient for Google Custom Search API
        builder.Services.AddHttpClient();

        // Register Phase 1 Enhanced Services
        builder.Services.AddScoped<AutomatedSimplifySearch>();
        builder.Services.AddScoped<NetDeveloperJobScorer>();
        builder.Services.AddScoped<IntelligentBulkProcessor>();
        
        // Register Phase 2 Enhanced Services
        builder.Services.AddScoped<SmartDeduplicationService>();
        builder.Services.AddScoped<ApplicationManagementService>();
        builder.Services.AddScoped<MarketIntelligenceService>();

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Job Scraping API v1.0");
                // Remove the empty RoutePrefix to use default /swagger
                // c.RoutePrefix = string.Empty;
            });
        }

        app.UseCors("AllowAll");
        //app.UseHttpsRedirection(); // Temporarily disabled for debugging
        app.UseAuthorization();
        app.MapControllers();

        Log.Information("Job Scraping Service started successfully");
        app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Job Scraping Service terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
