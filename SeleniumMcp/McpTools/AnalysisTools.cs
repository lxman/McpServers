using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services;
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
    JobQueueManager jobQueueManager,
    IEnhancedJobScrapingService scrapingService,
    ILogger<AnalysisTools> logger)
{
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running automated comprehensive search");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing jobs in bulk for {SearchTerm}", searchTerm);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing SimplifyJobs enhanced for {SearchTerm}", searchTerm);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deduplicating jobs");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error categorizing applications");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
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
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating market intelligence report");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    // ==================== JOB QUEUE TOOLS ====================

    [McpServerTool, DisplayName("start_bulk_job")]
    [Description("See skills/selenium/analysis/start_bulk_job.md only when using this tool")]
    public Task<string> start_bulk_job(
        string searchTerm,
        string location,
        int targetJobs = 20,
        int maxAgeInDays = 30,
        string userId = "bulk_user")
    {
        try
        {
            logger.LogInformation($"Starting bulk job for {searchTerm} in {location}");

            var request = new BulkProcessingRequest
            {
                SearchTerm = searchTerm,
                Location = location,
                TargetJobCount = targetJobs,
                MaxAgeInDays = maxAgeInDays,
                UserId = userId,
                ScoringProfile = new NetDeveloperScoringProfile()
            };

            // Start job in background
            string jobId = jobQueueManager.StartJob(request, async (jId, req, ct) =>
            {
                return await bulkProcessor.ProcessJobsBulkAsync(
                    req,
                    (batch, total, msg, partial) =>
                    {
                        // Update progress after each batch
                        jobQueueManager.UpdateProgress(jId, batch, total, msg, partial);
                    }, ct);
            });

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                jobId,
                status = "started",
                searchTerm,
                location,
                targetJobs,
                message = $"Bulk job started. Use check_job_status(jobId='{jobId}') to monitor progress."
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error starting bulk job for {searchTerm}");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("check_job_status")]
    [Description("See skills/selenium/analysis/check_job_status.md only when using this tool")]
    public Task<string> check_job_status(string jobId)
    {
        try
        {
            logger.LogDebug($"Checking status for job {jobId}");

            JobStatusResponse status = jobQueueManager.GetJobStatus(jobId);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = status.Found,
                status.JobId,
                status.Status,
                status.ProgressMessage,
                status.SearchTerm,
                status.Location,
                status.JobsProcessed,
                currentBatch = status.CurrentBatch,
                totalBatches = status.TotalBatches,
                status.ElapsedSeconds,
                status.IsComplete,
                summary = status.Summary,
                message = status.Found
                    ? $"Job {status.Status.ToLower()}: {status.ProgressMessage}"
                    : $"Job {jobId} not found"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error checking job status for {jobId}");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("cancel_job")]
    [Description("See skills/selenium/analysis/cancel_job.md only when using this tool")]
    public Task<string> cancel_job(string jobId)
    {
        try
        {
            logger.LogInformation($"Cancelling job {jobId}");

            JobCancellationResponse response = jobQueueManager.CancelJob(jobId);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                response.Success,
                response.Message,
                response.Status,
                jobsProcessed = response.JobsProcessed,
                partialResults = response.PartialResults,
                hasResults = response.PartialResults?.ProcessedJobs.Count > 0
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error cancelling job {jobId}");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("list_bulk_jobs")]
    [Description("See skills/selenium/analysis/list_bulk_jobs.md only when using this tool")]
    public Task<string> list_bulk_jobs(bool includeCompleted = true)
    {
        try
        {
            logger.LogDebug("Listing bulk jobs");

            List<JobSummary> jobs = jobQueueManager.ListJobs(includeCompleted);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                count = jobs.Count,
                jobs
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing bulk jobs");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("get_bulk_job_results")]
    [Description("See skills/selenium/analysis/get_bulk_job_results.md only when using this tool")]
    public Task<string> get_bulk_job_results(string jobId)
    {
        try
        {
            logger.LogInformation($"Retrieving full results for job {jobId}");

            JobStatusResponse status = jobQueueManager.GetJobStatus(jobId);

            if (!status.Found)
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Job {jobId} not found"
                }, SerializerOptions.JsonOptionsIndented));
            }

            if (!status.IsComplete)
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Job {jobId} is still {status.Status.ToLower()}. Use check_job_status to monitor progress.",
                    status = status.Status,
                    jobsProcessed = status.JobsProcessed,
                    summary = status.Summary
                }, SerializerOptions.JsonOptionsIndented));
            }

            BulkProcessingResult? result = jobQueueManager.GetJobResults(jobId);

            if (result == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Job {jobId} completed but results are no longer available"
                }, SerializerOptions.JsonOptionsIndented));
            }

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                jobId,
                status = status.Status,
                result
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error retrieving results for job {jobId}");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("consolidate_temporary_results")]
    [Description("See skills/selenium/storage/consolidate_temporary_results.md only when using this tool")]
    public async Task<string> consolidate_temporary_results(string sessionId, string userId)
    {
        try
        {
            logger.LogInformation($"Consolidating temporary results for session {sessionId}");

            ConsolidationResult result = await scrapingService.ConsolidateTemporaryResultsAsync(sessionId, userId);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                sessionId = result.SessionId,
                jobsConsolidated = result.JobsConsolidated,
                jobsSaved = result.JobsSaved,
                message = result.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error consolidating temporary results for session {sessionId}");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_temporary_results")]
    [Description("See skills/selenium/storage/get_temporary_results.md only when using this tool")]
    public async Task<string> get_temporary_results(string? sessionId = null, bool includeConsolidated = false)
    {
        try
        {
            logger.LogInformation($"Retrieving temporary results{(sessionId is not null ? $" for session {sessionId}" : " (all sessions)")}");

            List<TemporaryJobListing> tempResults = await scrapingService.GetTemporaryResultsAsync(sessionId, includeConsolidated);

            // Group by session for summary
            var sessionSummaries = tempResults
                .GroupBy(t => t.SessionId)
                .Select(g => new
                {
                    sessionId = g.Key,
                    jobCount = g.Count(),
                    batches = g.Select(t => t.BatchNumber).Distinct().Count(),
                    operationType = g.First().OperationType,
                    searchTerm = g.First().SearchTerm,
                    location = g.First().Location,
                    consolidated = g.First().Consolidated,
                    savedAt = g.Max(t => t.SavedAt)
                })
                .OrderByDescending(s => s.savedAt)
                .ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                totalResults = tempResults.Count,
                sessionCount = sessionSummaries.Count,
                sessions = sessionSummaries,
                // Include full job listings if requested for specific session and result set is small
                jobs = sessionId != null && tempResults.Count <= 50
                    ? tempResults.Select(t => new
                    {
                        sessionId = t.SessionId,
                        batchNumber = t.BatchNumber,
                        consolidated = t.Consolidated,
                        savedAt = t.SavedAt,
                        job = new
                        {
                            title = t.JobListing.Title,
                            company = t.JobListing.Company,
                            location = t.JobListing.Location,
                            matchScore = t.JobListing.MatchScore,
                            url = t.JobListing.Url
                        }
                    }).ToList()
                    : null
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving temporary results");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}