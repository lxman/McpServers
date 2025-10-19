using SeleniumChromeTool.Models;
using SeleniumChromeTool.Services.Scrapers;

namespace SeleniumChromeTool.Services.Enhanced;

/// <summary>
/// Phase 1 Enhancement: Bulk Processing for 10-20 jobs per session instead of current 2
/// </summary>
public class IntelligentBulkProcessor
{
    private readonly ILogger<IntelligentBulkProcessor> _logger;
    private readonly SimplifyJobsScraper _scraper;
    private readonly NetDeveloperJobScorer _scorer;

    public IntelligentBulkProcessor(
        ILogger<IntelligentBulkProcessor> logger,
        SimplifyJobsScraper scraper,
        NetDeveloperJobScorer scorer)
    {
        _logger = logger;
        _scraper = scraper;
        _scorer = scorer;
    }

    /// <summary>
    /// Process jobs in bulk with intelligent pagination and stopping criteria
    /// </summary>
    public async Task<BulkProcessingResult> ProcessJobsBulkAsync(BulkProcessingRequest request)
    {
        var result = new BulkProcessingResult
        {
            StartTime = DateTime.UtcNow,
            ProcessedJobs = [],
            SkippedJobs = [],
            Errors = []
        };

        try
        {
            _logger.LogInformation($"Starting bulk processing: target {request.TargetJobCount} jobs");

            SiteConfiguration config = _scraper.GetDefaultConfiguration();
            
            // Adaptive batch sizing based on target
            int batchSize = CalculateOptimalBatchSize(request.TargetJobCount);
            var currentPage = 1;
            var consecutiveLowScoreCount = 0;
            var maxConsecutiveLowScore = 3;

            while (result.ProcessedJobs.Count < request.TargetJobCount)
            {
                _logger.LogInformation($"Processing page {currentPage}, batch size {batchSize}");

                try
                {
                    // Create search request for current batch
                    var searchRequest = new EnhancedScrapeRequest
                    {
                        SearchTerm = request.SearchTerm,
                        Location = request.Location,
                        MaxResults = batchSize,
                        IncludeDescription = true,
                        MaxAgeInDays = request.MaxAgeInDays,
                        UserId = request.UserId
                    };

                    // Get jobs for current batch
                    List<EnhancedJobListing> jobs = await _scraper.ScrapeJobsAsync(searchRequest, config);
                    
                    if (jobs.Count == 0)
                    {
                        _logger.LogInformation("No more jobs found - ending bulk processing");
                        break;
                    }

                    _logger.LogInformation($"Retrieved {jobs.Count} jobs in batch {currentPage}");

                    var batchHighScoreCount = 0;

                    foreach (EnhancedJobListing job in jobs)
                    {
                        if (result.ProcessedJobs.Count >= request.TargetJobCount)
                            break;

                        try
                        {
                            // Enhanced scoring
                            JobScoringResult scoringResult = _scorer.CalculateEnhancedMatchScore(job, request.ScoringProfile);
                            job.MatchScore = scoringResult.TotalScore;

                            // Store scoring details
                            job.Notes = (job.Notes ?? "") + $" | Bulk Score: {scoringResult.TotalScore}% " +
                                       $"(Page: {currentPage}, Batch: {result.ProcessedJobs.Count + 1})";

                            result.ProcessedJobs.Add(job);

                            // Track high-scoring jobs in this batch
                            if (scoringResult.TotalScore >= 60)
                            {
                                batchHighScoreCount++;
                                _logger.LogInformation($"High-scoring job found: {job.Title} at {job.Company} ({scoringResult.TotalScore}%)");
                            }

                            // Log progress every 5 jobs
                            if (result.ProcessedJobs.Count % 5 == 0)
                            {
                                _logger.LogInformation($"Bulk progress: {result.ProcessedJobs.Count}/{request.TargetJobCount} jobs processed");
                            }
                        }
                        catch (Exception ex)
                        {
                            var error = $"Error processing job '{job.Title}' at '{job.Company}': {ex.Message}";
                            result.Errors.Add(error);
                            _logger.LogWarning(error);
                        }
                    }

                    // Intelligent stopping criteria
                    if (batchHighScoreCount == 0)
                    {
                        consecutiveLowScoreCount++;
                        _logger.LogInformation($"Batch {currentPage} had no high-scoring jobs ({consecutiveLowScoreCount}/{maxConsecutiveLowScore})");
                    }
                    else
                    {
                        consecutiveLowScoreCount = 0;
                    }

                    // Stop if we've had too many consecutive batches without good jobs
                    if (consecutiveLowScoreCount >= maxConsecutiveLowScore)
                    {
                        _logger.LogInformation($"Stopping bulk processing: {consecutiveLowScoreCount} consecutive batches with low scores");
                        break;
                    }

                    // Adaptive rate limiting
                    int delayMs = CalculateAdaptiveDelay(currentPage, jobs.Count);
                    await Task.Delay(delayMs);

                    currentPage++;
                }
                catch (Exception ex)
                {
                    var error = $"Error in batch {currentPage}: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.LogError(error);
                    
                    // Continue to next batch unless it's a critical error
                    if (ex is not TimeoutException)
                    {
                        currentPage++;
                        await Task.Delay(2000); // Extra delay after error
                    }
                    else
                    {
                        break; // Stop on timeout
                    }
                }
            }

            result.EndTime = DateTime.UtcNow;
            result.TotalDuration = result.EndTime - result.StartTime;
            result.PagesProcessed = currentPage - 1;

            // Calculate summary statistics
            result.HighPriorityCount = result.ProcessedJobs.Count(j => j.MatchScore >= 80);
            result.ApplicationReadyCount = result.ProcessedJobs.Count(j => j.MatchScore is >= 60 and < 80);
            result.ConsiderCount = result.ProcessedJobs.Count(j => j.MatchScore is >= 40 and < 60);
            result.LowPriorityCount = result.ProcessedJobs.Count(j => j.MatchScore < 40);

            _logger.LogInformation("Bulk processing completed:");
            _logger.LogInformation($"  Total Jobs: {result.ProcessedJobs.Count}");
            _logger.LogInformation($"  High Priority (80%+): {result.HighPriorityCount}");
            _logger.LogInformation($"  Application Ready (60%+): {result.ApplicationReadyCount}");
            _logger.LogInformation($"  Consider (40%+): {result.ConsiderCount}");
            _logger.LogInformation($"  Low Priority (<40%): {result.LowPriorityCount}");
            _logger.LogInformation($"  Pages Processed: {result.PagesProcessed}");
            _logger.LogInformation($"  Duration: {result.TotalDuration}");
            _logger.LogInformation($"  Errors: {result.Errors.Count}");

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.TotalDuration = result.EndTime - result.StartTime;
            
            var error = $"Critical error in bulk processing: {ex.Message}";
            result.Errors.Add(error);
            _logger.LogError(error);
            
            return result;
        }
    }

    private int CalculateOptimalBatchSize(int targetJobCount)
    {
        // Dynamic batch sizing based on target
        if (targetJobCount <= 10)
            return 5;
        if (targetJobCount <= 20)
            return 8;
        if (targetJobCount <= 50)
            return 10;
        return 15;
    }

    private int CalculateAdaptiveDelay(int pageNumber, int jobsInBatch)
    {
        // Base delay
        var baseDelay = 1000;

        // Reduce delay for early pages (less likely to be rate limited)
        if (pageNumber <= 3)
            baseDelay = 800;

        // Increase delay if fewer jobs returned (might indicate rate limiting)
        if (jobsInBatch < 3)
            baseDelay += 500;

        // Add some randomization to avoid patterns
        int randomization = new Random().Next(-200, 200);
        
        return Math.Max(500, baseDelay + randomization);
    }

    /// <summary>
    /// Process a single search with automatic pagination until targets are met
    /// </summary>
    public async Task<BulkProcessingResult> ProcessSingleSearchBulkAsync(string searchTerm, string location, int targetJobs = 20)
    {
        var request = new BulkProcessingRequest
        {
            SearchTerm = searchTerm,
            Location = location,
            TargetJobCount = targetJobs,
            MaxAgeInDays = 30,
            UserId = "bulk_processor",
            ScoringProfile = new NetDeveloperScoringProfile()
        };

        return await ProcessJobsBulkAsync(request);
    }
}

/// <summary>
/// Request model for bulk processing
/// </summary>
public class BulkProcessingRequest
{
    public string SearchTerm { get; set; } = "Senior .NET Developer";
    public string Location { get; set; } = "Remote in USA";
    public int TargetJobCount { get; set; } = 20;
    public int MaxAgeInDays { get; set; } = 30;
    public string UserId { get; set; } = "bulk_user";
    public NetDeveloperScoringProfile ScoringProfile { get; set; } = new();
}

/// <summary>
/// Result model for bulk processing with statistics
/// </summary>
public class BulkProcessingResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    
    public List<EnhancedJobListing> ProcessedJobs { get; set; } = [];
    public List<string> SkippedJobs { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    
    public int PagesProcessed { get; set; }
    public int HighPriorityCount { get; set; }
    public int ApplicationReadyCount { get; set; }
    public int ConsiderCount { get; set; }
    public int LowPriorityCount { get; set; }
    
    public double AverageScore => ProcessedJobs.Count > 0 ? ProcessedJobs.Average(j => j.MatchScore) : 0;
    public double JobsPerMinute => TotalDuration.TotalMinutes > 0 ? ProcessedJobs.Count / TotalDuration.TotalMinutes : 0;
    public bool IsSuccessful => Errors.Count == 0 && ProcessedJobs.Count > 0;
}
