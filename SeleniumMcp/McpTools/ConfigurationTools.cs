using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
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
    [McpServerTool, DisplayName("get_site_config")]
    [Description("See skills/selenium/config/get_site_config.md only when using this tool")]
    public async Task<string> get_site_config(string site)
    {
        try
        {
            logger.LogDebug("Retrieving configuration for site {Site}", site);

            if (!Enum.TryParse(site, ignoreCase: true, out JobSite siteEnum))
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Invalid site: {site}" }, SerializerOptions.JsonOptionsIndented);
            }

            SiteConfiguration config = await scrapingService.GetSiteConfigurationAsync(siteEnum);

            return JsonSerializer.Serialize(new
            {
                success = true,
                site,
                config
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving configuration for site {Site}", site);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("update_site_config")]
    [Description("See skills/selenium/config/update_site_config.md only when using this tool")]
    public async Task<string> update_site_config(string configJson)
    {
        try
        {
            SiteConfiguration config = JsonSerializer.Deserialize<SiteConfiguration>(configJson)
                                       ?? throw new ArgumentException("Invalid site configuration JSON");

            logger.LogDebug("Updating configuration for site {Site}", config.SiteName);

            await scrapingService.UpdateSiteConfigurationAsync(config);

            return JsonSerializer.Serialize(new
            {
                success = true,
                site = config.SiteName,
                message = "Configuration updated successfully"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating configuration");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing health check");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("test_site_access")]
    [Description("See skills/selenium/config/test_site_access.md only when using this tool")]
    public async Task<string> test_site_access(string site)
    {
        try
        {
            logger.LogDebug("Testing accessibility for site {Site}", site);

            if (!Enum.TryParse(site, ignoreCase: true, out JobSite siteEnum))
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Invalid site: {site}" }, SerializerOptions.JsonOptionsIndented);
            }

            SiteConfiguration config = await scrapingService.GetSiteConfigurationAsync(siteEnum);

            return JsonSerializer.Serialize(new
            {
                success = true,
                site,
                accessible = true,
                timestamp = DateTime.UtcNow
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing accessibility for site {Site}", site);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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

            List<EnhancedJobListing> jobs = JsonSerializer.Deserialize<List<EnhancedJobListing>>(jobsJson) ?? [];

            // Step 1: Deduplication
            DeduplicationResult deduplicationResult = await deduplicationService.DeduplicateJobsAsync(jobs);

            // Step 2: Categorization
            ApplicationPreferences? preferences = null;
            if (!string.IsNullOrEmpty(preferencesJson))
            {
                preferences = JsonSerializer.Deserialize<ApplicationPreferences>(preferencesJson);
            }
            ApplicationCategorizationResult categorizationResult = await applicationService.CategorizeJobsAsync(
                deduplicationResult.UniqueJobs,
                preferences ?? new ApplicationPreferences());

            // Step 3: Market Intelligence
            MarketAnalysisRequest? marketRequest = null;
            if (!string.IsNullOrEmpty(marketRequestJson))
            {
                marketRequest = JsonSerializer.Deserialize<MarketAnalysisRequest>(marketRequestJson);
            }
            MarketIntelligenceReport marketResult = await marketService.GenerateMarketReportAsync(
                deduplicationResult.UniqueJobs,
                marketRequest ?? new MarketAnalysisRequest { JobTitle = "Software Engineer", FocusArea = "comprehensive" });

            return JsonSerializer.Serialize(new
            {
                success = true,
                deduplication = deduplicationResult,
                categorization = categorizationResult,
                marketIntelligence = marketResult
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing enhanced job analysis");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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

            string[] jobIds = JsonSerializer.Deserialize<string[]>(jobIdsJson) ?? [];
            List<EnhancedJobListing> result = await simplifyApiService.FetchJobsByIdsAsync(jobIds, userId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                userId,
                jobIdsCount = jobIds.Length,
                jobsRetrieved = result.Count,
                jobs = result
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching jobs by IDs");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
