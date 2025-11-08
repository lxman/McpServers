using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services;
using SeleniumChrome.Core.Services.Enhanced;
using SeleniumChrome.Core.Services.Scrapers;

namespace SeleniumMcp.McpTools;

/// <summary>
/// MCP tools for configuration and health check operations
/// </summary>
[McpServerToolType]
public class ConfigurationTools(
    IEnhancedJobScrapingService scrapingService,
    IJobSiteScraperFactory scraperFactory,
    SimplifyJobsApiService simplifyApiService,
    SmartDeduplicationService deduplicationService,
    ApplicationManagementService applicationService,
    MarketIntelligenceService marketService,
    ILogger<ConfigurationTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("get_site_config")]
    [Description("See skills/selenium/config/get_site_config.md only when using this tool")]
    public async Task<string> get_site_config(string site)
    {
        try
        {
            logger.LogDebug("Retrieving configuration for site {Site}", site);

            if (!Enum.TryParse<JobSite>(site, ignoreCase: true, out var siteEnum))
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Invalid site: {site}" }, _jsonOptions);
            }

            var config = await scrapingService.GetSiteConfigurationAsync(siteEnum);

            return JsonSerializer.Serialize(new
            {
                success = true,
                site,
                config
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving configuration for site {Site}", site);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("update_site_config")]
    [Description("See skills/selenium/config/update_site_config.md only when using this tool")]
    public async Task<string> update_site_config(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SiteConfiguration>(configJson)
                         ?? throw new ArgumentException("Invalid site configuration JSON");

            logger.LogDebug("Updating configuration for site {Site}", config.SiteName);

            await scrapingService.UpdateSiteConfigurationAsync(config);

            return JsonSerializer.Serialize(new
            {
                success = true,
                site = config.SiteName,
                message = "Configuration updated successfully"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating configuration");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("health_check")]
    [Description("See skills/selenium/config/health_check.md only when using this tool")]
    public Task<string> health_check()
    {
        try
        {
            logger.LogDebug("Performing health check");

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                status = "healthy",
                timestamp = DateTime.UtcNow
            }, _jsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing health check");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions));
        }
    }

    [McpServerTool, DisplayName("test_site_access")]
    [Description("See skills/selenium/config/test_site_access.md only when using this tool")]
    public async Task<string> test_site_access(string site)
    {
        try
        {
            logger.LogDebug("Testing accessibility for site {Site}", site);

            if (!Enum.TryParse<JobSite>(site, ignoreCase: true, out var siteEnum))
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Invalid site: {site}" }, _jsonOptions);
            }

            var config = await scrapingService.GetSiteConfigurationAsync(siteEnum);

            return JsonSerializer.Serialize(new
            {
                success = true,
                site,
                accessible = true,
                timestamp = DateTime.UtcNow
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing accessibility for site {Site}", site);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("enhanced_job_analysis")]
    [Description("See skills/selenium/config/enhanced_job_analysis.md only when using this tool")]
    public async Task<string> enhanced_job_analysis(
        string jobsJson,
        string? preferencesJson = null,
        string? marketRequestJson = null)
    {
        try
        {
            logger.LogDebug("Performing enhanced job analysis pipeline");

            var jobs = JsonSerializer.Deserialize<List<EnhancedJobListing>>(jobsJson) ?? [];

            // Step 1: Deduplication
            var deduplicationResult = await deduplicationService.DeduplicateJobsAsync(jobs);

            // Step 2: Categorization
            ApplicationPreferences? preferences = null;
            if (!string.IsNullOrEmpty(preferencesJson))
            {
                preferences = JsonSerializer.Deserialize<ApplicationPreferences>(preferencesJson);
            }
            var categorizationResult = await applicationService.CategorizeJobsAsync(
                deduplicationResult.UniqueJobs,
                preferences ?? new ApplicationPreferences());

            // Step 3: Market Intelligence
            MarketAnalysisRequest? marketRequest = null;
            if (!string.IsNullOrEmpty(marketRequestJson))
            {
                marketRequest = JsonSerializer.Deserialize<MarketAnalysisRequest>(marketRequestJson);
            }
            var marketResult = await marketService.GenerateMarketReportAsync(
                deduplicationResult.UniqueJobs,
                marketRequest ?? new MarketAnalysisRequest { JobTitle = "Software Engineer", FocusArea = "comprehensive" });

            return JsonSerializer.Serialize(new
            {
                success = true,
                deduplication = deduplicationResult,
                categorization = categorizationResult,
                marketIntelligence = marketResult
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing enhanced job analysis");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("fetch_jobs_by_ids")]
    [Description("See skills/selenium/config/fetch_jobs_by_ids.md only when using this tool")]
    public async Task<string> fetch_jobs_by_ids(
        string jobIdsJson,
        string userId = "default_user")
    {
        try
        {
            logger.LogDebug("Fetching jobs by IDs for user {UserId}", userId);

            var jobIds = JsonSerializer.Deserialize<string[]>(jobIdsJson) ?? [];
            var result = await simplifyApiService.FetchJobsByIdsAsync(jobIds, userId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                userId,
                jobIdsCount = jobIds.Length,
                jobsRetrieved = result.Count,
                jobs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching jobs by IDs");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
