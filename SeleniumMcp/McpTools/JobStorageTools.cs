using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services;

namespace SeleniumMcp.McpTools;

/// <summary>
/// MCP tools for job storage and retrieval operations
/// </summary>
[McpServerToolType]
public class JobStorageTools(
    IEnhancedJobScrapingService scrapingService,
    ILogger<JobStorageTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("save_jobs")]
    [Description("See skills/selenium/storage/save_jobs.md only when using this tool")]
    public async Task<string> save_jobs(
        string jobsJson,
        string userId,
        bool overwriteExisting = false)
    {
        try
        {
            logger.LogDebug("Saving jobs for user {UserId}", userId);

            List<EnhancedJobListing> jobs = JsonSerializer.Deserialize<List<EnhancedJobListing>>(jobsJson) ?? [];

            var request = new SaveJobsRequest
            {
                UserId = userId,
                Jobs = jobs,
                OverwriteExisting = overwriteExisting
            };

            bool result = await scrapingService.SaveJobsAsync(request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                userId,
                jobsCount = jobs.Count,
                saved = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving jobs for user {UserId}", userId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_stored_jobs")]
    [Description("See skills/selenium/storage/get_stored_jobs.md only when using this tool")]
    public async Task<string> get_stored_jobs(
        string userId,
        string? sitesJson = null,
        string? fromDate = null,
        string? toDate = null,
        bool? isRemote = null,
        double? minMatchScore = null,
        bool? isApplied = null,
        string? requiredSkillsJson = null)
    {
        try
        {
            logger.LogDebug("Retrieving stored jobs for user {UserId}", userId);

            var filters = new JobSearchFilters();

            if (!string.IsNullOrEmpty(sitesJson))
            {
                filters.Sites = JsonSerializer.Deserialize<List<JobSite>>(sitesJson) ?? [];
            }

            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out DateTime fromDateTime))
            {
                filters.FromDate = fromDateTime;
            }

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out DateTime toDateTime))
            {
                filters.ToDate = toDateTime;
            }

            filters.IsRemote = isRemote;
            filters.MinMatchScore = minMatchScore;
            filters.IsApplied = isApplied;

            if (!string.IsNullOrEmpty(requiredSkillsJson))
            {
                filters.RequiredSkills = JsonSerializer.Deserialize<List<string>>(requiredSkillsJson) ?? [];
            }

            List<EnhancedJobListing> result = await scrapingService.GetStoredJobsAsync(userId, filters);

            return JsonSerializer.Serialize(new
            {
                success = true,
                userId,
                filters,
                jobsCount = result.Count,
                jobs = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving stored jobs for user {UserId}", userId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_screenshot")]
    [Description("See skills/selenium/storage/get_screenshot.md only when using this tool")]
    public async Task<string> get_screenshot(string url)
    {
        try
        {
            logger.LogDebug("Taking screenshot for URL {Url}", url);

            string result = await scrapingService.TakeScreenshotAsync(url);

            return JsonSerializer.Serialize(new
            {
                success = true,
                url,
                filePath = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error taking screenshot for URL {Url}", url);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
