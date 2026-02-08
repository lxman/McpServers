using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services;
using SeleniumChrome.Core.Services.Scrapers;

namespace SeleniumMcp.McpTools;

/// <summary>
/// MCP tools for job scraping operations
/// </summary>
[McpServerToolType]
public class JobScrapingTools(
    IEnhancedJobScrapingService scrapingService,
    GoogleSimplifyJobsService googleService,
    ILogger<JobScrapingTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("scrape_multiple_sites")]
    [Description("See skills/selenium/scraping/scrape_multiple_sites.md only when using this tool")]
    public async Task<string> scrape_multiple_sites(
        string sitesJson,
        string searchTerm,
        string location,
        string userId = "default_user",
        int maxResults = 50,
        int maxAgeInDays = 30)
    {
        try
        {
            logger.LogDebug("Scraping multiple sites for {SearchTerm} in {Location}", searchTerm, location);

            List<JobSite> sites = JsonSerializer.Deserialize<List<JobSite>>(sitesJson) ?? [];

            var request = new EnhancedScrapeRequest
            {
                SearchTerm = searchTerm,
                Location = location,
                MaxResults = maxResults,
                Sites = sites,
                MaxAgeInDays = maxAgeInDays,
                UserId = userId
            };

            List<EnhancedJobListing> result = await scrapingService.ScrapeMultipleSitesAsync(request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                searchTerm,
                location,
                siteCount = sites.Count,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scraping multiple sites for {SearchTerm} in {Location}", searchTerm, location);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("scrape_site")]
    [Description("See skills/selenium/scraping/scrape_site.md only when using this tool")]
    public async Task<string> scrape_site(
        string site,
        string searchTerm,
        string location,
        string userId = "default_user",
        int maxResults = 50,
        int maxAgeInDays = 30)
    {
        try
        {
            logger.LogDebug("Scraping site {Site} for {SearchTerm} in {Location}", site, searchTerm, location);

            if (!Enum.TryParse(site, ignoreCase: true, out JobSite jobSite))
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Invalid site: {site}" }, _jsonOptions);
            }

            var request = new EnhancedScrapeRequest
            {
                SearchTerm = searchTerm,
                Location = location,
                MaxResults = maxResults,
                MaxAgeInDays = maxAgeInDays,
                UserId = userId
            };

            List<EnhancedJobListing> result = await scrapingService.ScrapeSpecificSiteAsync(jobSite, request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                site,
                searchTerm,
                location,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scraping site {Site} for {SearchTerm} in {Location}", site, searchTerm, location);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("simplify_jobs_google")]
    [Description("See skills/selenium/scraping/simplify_jobs_google.md only when using this tool")]
    public async Task<string> simplify_jobs_google(
        string searchTerm,
        string location,
        int maxResults = 20,
        string userId = "default_user",
        int maxAgeInDays = 30)
    {
        try
        {
            logger.LogDebug("Discovering SimplifyJobs via Google for {SearchTerm} in {Location}", searchTerm, location);

            var request = new EnhancedScrapeRequest
            {
                SearchTerm = searchTerm,
                Location = location,
                MaxResults = maxResults,
                MaxAgeInDays = maxAgeInDays,
                UserId = userId
            };

            List<EnhancedJobListing> result = await googleService.DiscoverAndFetchJobsAsync(request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                searchTerm,
                location,
                maxResults,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error discovering SimplifyJobs via Google for {SearchTerm} in {Location}", searchTerm, location);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
