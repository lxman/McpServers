using Microsoft.Extensions.Logging;
using SeleniumChrome.Core.Models;
using SeleniumChrome.Core.Services.Scrapers;

namespace SeleniumChrome.Core.Services.Enhanced;

/// <summary>
/// Bulk Processing for 10-20 jobs per session instead of current 2
/// </summary>
public class IntelligentBulkProcessor(
    ILogger<IntelligentBulkProcessor> logger,
    SimplifyJobsScraper scraper,
    NetDeveloperJobScorer scorer,
    EnhancedJobScrapingService scrapingService)
{
    /// <summary>
    /// Process jobs in bulk with intelligent pagination and stopping criteria
    /// ENHANCED: Now supports cancellation tokens and progress callbacks
    /// Progress callback uses lightweight summary to avoid token explosion
    /// AUTO-SAVES: Each batch is automatically saved to temporary collection for recovery
    /// </summary>
    public async Task<BulkProcessingResult> ProcessJobsBulkAsync(BulkProcessingRequest request,
        Action<int, int, string, BulkProcessingSummary>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? sessionId = null)
    {
        var result = new BulkProcessingResult
        {
            StartTime = DateTime.UtcNow,
            ProcessedJobs = [],
            SkippedJobs = [],
            Errors = []
        };

        // Generate sessionId if not provided for temporary storage
        string effectiveSessionId = sessionId ?? Guid.NewGuid().ToString();

        try
        {
            logger.LogInformation($"Starting bulk processing: target {request.TargetJobCount} jobs (session: {effectiveSessionId})");

            SiteConfiguration config = scraper.GetDefaultConfiguration();

            // Adaptive batch sizing based on target
            int batchSize = CalculateOptimalBatchSize(request.TargetJobCount);
            var currentPage = 1;
            var consecutiveLowScoreCount = 0;
            const int maxConsecutiveLowScore = 3;
            var estimatedTotalBatches = (int)Math.Ceiling(request.TargetJobCount / (double)batchSize);

            while (result.ProcessedJobs.Count < request.TargetJobCount)
            {
                // CHECK CANCELLATION BEFORE EACH BATCH
                cancellationToken.ThrowIfCancellationRequested();

                logger.LogInformation($"Processing page {currentPage}, batch size {batchSize}");

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
                    List<EnhancedJobListing> jobs = await scraper.ScrapeJobsAsync(searchRequest, config);
                    
                    if (jobs.Count == 0)
                    {
                        logger.LogInformation("No more jobs found - ending bulk processing");
                        break;
                    }

                    logger.LogInformation($"Retrieved {jobs.Count} jobs in batch {currentPage}");

                    var batchHighScoreCount = 0;

                    foreach (EnhancedJobListing job in jobs)
                    {
                        // CHECK CANCELLATION DURING BATCH PROCESSING
                        cancellationToken.ThrowIfCancellationRequested();

                        if (result.ProcessedJobs.Count >= request.TargetJobCount)
                            break;

                        try
                        {
                            // Enhanced scoring
                            JobScoringResult scoringResult = scorer.CalculateEnhancedMatchScore(job, request.ScoringProfile);
                            job.MatchScore = scoringResult.TotalScore;

                            // Store scoring details
                            job.Notes = (job.Notes ?? "") + $" | Bulk Score: {scoringResult.TotalScore}% " +
                                       $"(Page: {currentPage}, Batch: {result.ProcessedJobs.Count + 1})";

                            result.ProcessedJobs.Add(job);

                            // Track high-scoring jobs in this batch
                            if (scoringResult.TotalScore >= 60)
                            {
                                batchHighScoreCount++;
                                logger.LogInformation($"High-scoring job found: {job.Title} at {job.Company} ({scoringResult.TotalScore}%)");
                            }

                            // Log progress every 5 jobs
                            if (result.ProcessedJobs.Count % 5 == 0)
                            {
                                logger.LogInformation($"Bulk progress: {result.ProcessedJobs.Count}/{request.TargetJobCount} jobs processed");
                            }
                        }
                        catch (Exception ex)
                        {
                            var error = $"Error processing job '{job.Title}' at '{job.Company}': {ex.Message}";
                            result.Errors.Add(error);
                            logger.LogWarning(error);
                        }
                    }

                    // AUTO-SAVE BATCH TO TEMPORARY COLLECTION for recovery
                    // Get jobs processed in this batch (last N jobs added)
                    List<EnhancedJobListing> batchJobs = result.ProcessedJobs.Skip(Math.Max(0, result.ProcessedJobs.Count - jobs.Count)).ToList();
                    await scrapingService.SaveToTemporaryCollectionAsync(
                        batchJobs,
                        effectiveSessionId,
                        currentPage,
                        "bulk",
                        request.SearchTerm,
                        request.Location);

                    // REPORT PROGRESS AFTER EACH BATCH (lightweight summary only)
                    if (progressCallback != null)
                    {
                        // Create lightweight summary instead of copying all jobs
                        result.PagesProcessed = currentPage;
                        BulkProcessingSummary summary = BulkProcessingSummary.FromResult(result, result.StartTime);

                        // Update estimated total batches based on progress
                        if (result.ProcessedJobs.Count > 0)
                        {
                            double jobsPerBatch = result.ProcessedJobs.Count / (double)currentPage;
                            estimatedTotalBatches = Math.Max(estimatedTotalBatches,
                                (int)Math.Ceiling(request.TargetJobCount / jobsPerBatch));
                        }

                        progressCallback(
                            currentPage,
                            estimatedTotalBatches,
                            $"Batch {currentPage}/{estimatedTotalBatches}: {result.ProcessedJobs.Count}/{request.TargetJobCount} jobs, avg score: {summary.AverageScore:F1}",
                            summary
                        );
                    }

                    // Intelligent stopping criteria
                    if (batchHighScoreCount == 0)
                    {
                        consecutiveLowScoreCount++;
                        logger.LogInformation($"Batch {currentPage} had no high-scoring jobs ({consecutiveLowScoreCount}/{maxConsecutiveLowScore})");
                    }
                    else
                    {
                        consecutiveLowScoreCount = 0;
                    }

                    // Stop if we've had too many consecutive batches without good jobs
                    if (consecutiveLowScoreCount >= maxConsecutiveLowScore)
                    {
                        logger.LogInformation($"Stopping bulk processing: {consecutiveLowScoreCount} consecutive batches with low scores");
                        break;
                    }

                    // Adaptive rate limiting
                    int delayMs = CalculateAdaptiveDelay(currentPage, jobs.Count);
                    await Task.Delay(delayMs, cancellationToken);

                    currentPage++;
                }
                catch (OperationCanceledException)
                {
                    // Re-throw cancellation to be handled by JobQueueManager
                    logger.LogInformation($"Bulk processing cancelled at page {currentPage}");
                    throw;
                }
                catch (Exception ex)
                {
                    var error = $"Error in batch {currentPage}: {ex.Message}";
                    result.Errors.Add(error);
                    logger.LogError(error);
                    
                    // Continue to next batch unless it's a critical error
                    if (ex is not TimeoutException)
                    {
                        currentPage++;
                        await Task.Delay(2000, cancellationToken); // Extra delay after error
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

            logger.LogInformation("Bulk processing completed:");
            logger.LogInformation($"  Total Jobs: {result.ProcessedJobs.Count}");
            logger.LogInformation($"  High Priority (80%+): {result.HighPriorityCount}");
            logger.LogInformation($"  Application Ready (60%+): {result.ApplicationReadyCount}");
            logger.LogInformation($"  Consider (40%+): {result.ConsiderCount}");
            logger.LogInformation($"  Low Priority (<40%): {result.LowPriorityCount}");
            logger.LogInformation($"  Pages Processed: {result.PagesProcessed}");
            logger.LogInformation($"  Duration: {result.TotalDuration}");
            logger.LogInformation($"  Errors: {result.Errors.Count}");

            return result;
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation gracefully
            result.EndTime = DateTime.UtcNow;
            result.TotalDuration = result.EndTime - result.StartTime;
            result.PagesProcessed = Math.Max(0, result.ProcessedJobs.Count / CalculateOptimalBatchSize(request.TargetJobCount));

            logger.LogInformation($"Bulk processing cancelled: {result.ProcessedJobs.Count} jobs processed before cancellation");
            
            // Re-throw so JobQueueManager can handle it
            throw;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.TotalDuration = result.EndTime - result.StartTime;
            
            var error = $"Critical error in bulk processing: {ex.Message}";
            result.Errors.Add(error);
            logger.LogError(error);
            
            return result;
        }
    }

    private static int CalculateOptimalBatchSize(int targetJobCount)
    {
        return targetJobCount switch
        {
            // Dynamic batch sizing based on target
            <= 10 => 5,
            <= 20 => 8,
            <= 50 => 10,
            _ => 15
        };
    }

    private static int CalculateAdaptiveDelay(int pageNumber, int jobsInBatch)
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

/// <summary>
/// Lightweight summary for progress tracking (avoids token explosion)
/// Contains only statistics, no job objects
/// </summary>
public class BulkProcessingSummary
{
    public int JobsProcessed { get; set; }
    public double AverageScore { get; set; }
    public int HighPriorityCount { get; set; }  // 90-100
    public int GreatCount { get; set; }         // 75-89
    public int GoodCount { get; set; }          // 60-74
    public int FairCount { get; set; }          // <60
    public int ErrorCount { get; set; }
    public int PagesProcessed { get; set; }
    public double ElapsedSeconds { get; set; }

    /// <summary>
    /// Create summary from full result
    /// </summary>
    public static BulkProcessingSummary FromResult(BulkProcessingResult result, DateTime startTime)
    {
        return new BulkProcessingSummary
        {
            JobsProcessed = result.ProcessedJobs.Count,
            AverageScore = result.ProcessedJobs.Count > 0 ? result.ProcessedJobs.Average(j => j.MatchScore) : 0,
            HighPriorityCount = result.ProcessedJobs.Count(j => j.MatchScore >= 90),
            GreatCount = result.ProcessedJobs.Count(j => j.MatchScore >= 75 && j.MatchScore < 90),
            GoodCount = result.ProcessedJobs.Count(j => j.MatchScore >= 60 && j.MatchScore < 75),
            FairCount = result.ProcessedJobs.Count(j => j.MatchScore < 60),
            ErrorCount = result.Errors.Count,
            PagesProcessed = result.PagesProcessed,
            ElapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds
        };
    }
}