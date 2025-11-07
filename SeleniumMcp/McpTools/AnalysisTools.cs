using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services.Enhanced;

namespace SeleniumMcp.McpTools;

/// <summary>
/// MCP tools for job analysis operations
/// </summary>
[McpServerToolType]
public class AnalysisTools(
    AutomatedSimplifySearch automatedSearch,
    IntelligentBulkProcessor bulkProcessor,
    SmartDeduplicationService deduplicationService,
    ApplicationManagementService applicationService,
    MarketIntelligenceService marketService,
    ILogger<AnalysisTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("automated_comprehensive_search")]
    [Description("See skills/selenium/analysis/automated_comprehensive_search.md only when using this tool")]
    public async Task<string> automated_comprehensive_search(
        string searchTermsJson,
        string locationsJson,
        int targetJobsPerSearch = 20,
        int maxTotalResults = 50,
        int maxAgeInDays = 30,
        string userId = "automated_search_user")
    {
        try
        {
            logger.LogDebug("Starting automated comprehensive search with {TargetJobsPerSearch} target jobs per search", targetJobsPerSearch);

            List<string> searchTerms = JsonSerializer.Deserialize<List<string>>(searchTermsJson) ?? [];
            List<string> locations = JsonSerializer.Deserialize<List<string>>(locationsJson) ?? [];

            var request = new ComprehensiveSearchRequest
            {
                CustomSearchTerms = searchTerms,
                CustomLocations = locations,
                JobsPerSearch = targetJobsPerSearch,
                MaxTotalResults = maxTotalResults,
                MaxAgeInDays = maxAgeInDays,
                UserId = userId,
                ScoringProfile = new NetDeveloperScoringProfile()
            };

            EnhancedSearchResults result = await automatedSearch.RunComprehensiveNetSearchAsync(request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                searchTermsCount = searchTerms.Count,
                locationsCount = locations.Count,
                targetJobsPerSearch,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running automated comprehensive search");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("bulk_process_jobs")]
    [Description("See skills/selenium/analysis/bulk_process_jobs.md only when using this tool")]
    public async Task<string> bulk_process_jobs(
        string searchTerm,
        string location,
        int targetJobs = 20,
        int maxAgeInDays = 30,
        string userId = "bulk_user")
    {
        try
        {
            logger.LogDebug("Processing jobs in bulk for {SearchTerm} in {Location}", searchTerm, location);

            var request = new BulkProcessingRequest
            {
                SearchTerm = searchTerm,
                Location = location,
                TargetJobCount = targetJobs,
                MaxAgeInDays = maxAgeInDays,
                UserId = userId,
                ScoringProfile = new NetDeveloperScoringProfile()
            };

            BulkProcessingResult result = await bulkProcessor.ProcessJobsBulkAsync(request);

            return JsonSerializer.Serialize(new
            {
                success = true,
                searchTerm,
                location,
                targetJobs,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing jobs in bulk for {SearchTerm}", searchTerm);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("simplify_jobs_enhanced")]
    [Description("See skills/selenium/analysis/simplify_jobs_enhanced.md only when using this tool")]
    public async Task<string> simplify_jobs_enhanced(
        string searchTerm,
        string location,
        int maxResults = 50,
        int maxAgeInDays = 30,
        string userId = "bulk_user")
    {
        try
        {
            logger.LogDebug("Processing SimplifyJobs with enhanced scoring for {SearchTerm} in {Location}", searchTerm, location);

            var request = new BulkProcessingRequest
            {
                SearchTerm = searchTerm,
                Location = location,
                TargetJobCount = Math.Min(maxResults, 20),
                MaxAgeInDays = maxAgeInDays,
                UserId = userId,
                ScoringProfile = new NetDeveloperScoringProfile()
            };

            BulkProcessingResult result = await bulkProcessor.ProcessJobsBulkAsync(request);

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
            logger.LogError(ex, "Error processing SimplifyJobs enhanced for {SearchTerm}", searchTerm);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("smart_deduplication")]
    [Description("See skills/selenium/analysis/smart_deduplication.md only when using this tool")]
    public async Task<string> smart_deduplication(string jobsJson)
    {
        try
        {
            logger.LogDebug("Deduplicating jobs using smart deduplication service");

            List<EnhancedJobListing> jobs = JsonSerializer.Deserialize<List<EnhancedJobListing>>(jobsJson) ?? [];
            DeduplicationResult result = await deduplicationService.DeduplicateJobsAsync(jobs);

            return JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deduplicating jobs");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("categorize_applications")]
    [Description("See skills/selenium/analysis/categorize_applications.md only when using this tool")]
    public async Task<string> categorize_applications(
        string jobsJson,
        string? preferencesJson = null)
    {
        try
        {
            logger.LogDebug("Categorizing applications");

            List<EnhancedJobListing> jobs = JsonSerializer.Deserialize<List<EnhancedJobListing>>(jobsJson) ?? [];
            ApplicationPreferences? preferences = null;

            if (!string.IsNullOrEmpty(preferencesJson))
            {
                preferences = JsonSerializer.Deserialize<ApplicationPreferences>(preferencesJson);
            }

            ApplicationCategorizationResult result = await applicationService.CategorizeJobsAsync(jobs, preferences ?? new ApplicationPreferences());

            return JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error categorizing applications");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("market_intelligence")]
    [Description("See skills/selenium/analysis/market_intelligence.md only when using this tool")]
    public async Task<string> market_intelligence(
        string jobsJson,
        string? requestJson = null)
    {
        try
        {
            logger.LogDebug("Generating market intelligence report");

            List<EnhancedJobListing> jobs = JsonSerializer.Deserialize<List<EnhancedJobListing>>(jobsJson) ?? [];
            MarketAnalysisRequest? request = null;

            if (!string.IsNullOrEmpty(requestJson))
            {
                request = JsonSerializer.Deserialize<MarketAnalysisRequest>(requestJson);
            }

            MarketIntelligenceReport result = await marketService.GenerateMarketReportAsync(
                jobs,
                request ?? new MarketAnalysisRequest { JobTitle = "Software Engineer", FocusArea = "comprehensive" });

            return JsonSerializer.Serialize(new
            {
                success = true,
                report = result
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating market intelligence report");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}
