using Mcp.Database.Core.MongoDB;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services;

public class EnhancedJobScrapingService(
    IJobSiteScraperFactory scraperFactory,
    MongoConnectionManager connectionManager,
    ILogger<EnhancedJobScrapingService> logger)
    : IEnhancedJobScrapingService
{
    private const string DEFAULT_CONNECTION_NAME = "default";

    private IMongoDatabase GetDatabase() => connectionManager.GetDatabase(DEFAULT_CONNECTION_NAME)
        ?? throw new InvalidOperationException("MongoDB connection not found. Please ensure MongoDB is configured.");

    private IMongoCollection<EnhancedJobListing> JobListings => GetDatabase().GetCollection<EnhancedJobListing>("search_results");
    private IMongoCollection<TemporaryJobListing> TempJobListings => GetDatabase().GetCollection<TemporaryJobListing>("search_results_temp");
    private IMongoCollection<SiteConfiguration> SiteConfigurations => GetDatabase().GetCollection<SiteConfiguration>("site_configurations");
    private IWebDriver? _screenshotDriver;

    // Service-level semaphore to prevent concurrent scraping operations
    private static readonly SemaphoreSlim ScrapingSemaphore = new(1, 1);

    public async Task<List<EnhancedJobListing>> ScrapeMultipleSitesAsync(EnhancedScrapeRequest request, string? sessionId = null)
    {
        // Generate sessionId if not provided for temporary storage
        string effectiveSessionId = sessionId ?? Guid.NewGuid().ToString();

        logger.LogInformation($"Starting multi-site scrape for {request.Sites.Count} sites (session: {effectiveSessionId})");

        var allJobs = new List<EnhancedJobListing>();
        var tasks = new List<Task<List<EnhancedJobListing>>>();

        foreach (JobSite site in request.Sites)
        {
            try
            {
                List<JobSite> supportedSites = scraperFactory.GetSupportedSites();
                if (!supportedSites.Contains(site))
                {
                    logger.LogWarning($"Scraper for {site} is not yet implemented");
                    continue;
                }

                // Pass sessionId to group all results from this multi-site operation
                tasks.Add(ScrapeSpecificSiteAsync(site, request, effectiveSessionId));
            }
            catch (Exception ex)
            {
                logger.LogError($"Error setting up scraper for {site}: {ex.Message}");
            }
        }

        List<EnhancedJobListing>[] results = await Task.WhenAll(tasks);

        foreach (List<EnhancedJobListing> siteJobs in results)
        {
            allJobs.AddRange(siteJobs);
        }

        // Calculate match scores
        await CalculateMatchScores(allJobs, request.UserId);

        // AUTO-SAVE COMBINED RESULTS TO TEMPORARY COLLECTION after scoring
        // This creates a final batch with all scored results for easy recovery
        await SaveToTemporaryCollectionAsync(
            allJobs,
            effectiveSessionId,
            99, // High batch number to appear after individual site saves
            "multi_site_combined",
            request.SearchTerm,
            request.Location);

        // Note: Jobs are not automatically saved to final collection - use SaveJobsAsync after filtering

        logger.LogInformation($"Successfully scraped {allJobs.Count} total jobs from {request.Sites.Count} sites");
        return allJobs.OrderByDescending(j => j.MatchScore).ToList();
    }

    public async Task<List<EnhancedJobListing>> ScrapeSpecificSiteAsync(JobSite site, EnhancedScrapeRequest request, string? sessionId = null)
    {
        // Generate sessionId if not provided for temporary storage
        string effectiveSessionId = sessionId ?? Guid.NewGuid().ToString();

        logger.LogInformation($"Requesting scraping lock for {site} (session: {effectiveSessionId})...");

        // Prevent concurrent scraping operations
        TimeSpan timeout = TimeSpan.FromMinutes(3);
        if (!await ScrapingSemaphore.WaitAsync(timeout))
        {
            logger.LogError($"Failed to acquire scraping lock for {site} within timeout");
            throw new TimeoutException($"Scraping lock timeout for {site}");
        }

        try
        {
            logger.LogInformation($"Acquired scraping lock for {site}");

            IJobSiteScraper scraper = scraperFactory.CreateScraper(site);
            SiteConfiguration config = await GetSiteConfigurationAsync(site);

            logger.LogInformation($"Starting scrape of {site} with config last updated: {config.LastUpdated}");

            List<EnhancedJobListing> jobs = await scraper.ScrapeJobsAsync(request, config);

            logger.LogInformation($"Successfully scraped {jobs.Count} jobs from {site}");

            // AUTO-SAVE TO TEMPORARY COLLECTION for recovery
            await SaveToTemporaryCollectionAsync(
                jobs,
                effectiveSessionId,
                1, // Single batch for single-site scraping
                "single_site",
                request.SearchTerm,
                request.Location);

            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error scraping {site}: {ex.Message}");
            return [];
        }
        finally
        {
            ScrapingSemaphore.Release();
            logger.LogInformation($"Released scraping lock for {site}");
        }
    }

    public async Task<SiteConfiguration> GetSiteConfigurationAsync(JobSite site)
    {
        FilterDefinition<SiteConfiguration>? filter = Builders<SiteConfiguration>.Filter.Eq(s => s.SiteName, site.ToString());
        SiteConfiguration? config = await SiteConfigurations.Find(filter).FirstOrDefaultAsync();

        if (config is null)
        {
            // Create default configuration using the scraper
            IJobSiteScraper scraper = scraperFactory.CreateScraper(site);
            config = scraper.GetDefaultConfiguration();
            await SiteConfigurations.InsertOneAsync(config);
            logger.LogInformation($"Created default configuration for {site}");
        }

        return config;
    }

    public async Task UpdateSiteConfigurationAsync(SiteConfiguration config)
    {
        config.LastUpdated = DateTime.UtcNow;
        FilterDefinition<SiteConfiguration>? filter = Builders<SiteConfiguration>.Filter.Eq(s => s.SiteName, config.SiteName);
        await SiteConfigurations.ReplaceOneAsync(filter, config, new ReplaceOptions { IsUpsert = true });
        logger.LogInformation($"Updated configuration for {config.SiteName}");
    }

    public async Task<List<EnhancedJobListing>> GetStoredJobsAsync(string userId, JobSearchFilters filters)
    {
        FilterDefinitionBuilder<EnhancedJobListing>? filterBuilder = Builders<EnhancedJobListing>.Filter;
        FilterDefinition<EnhancedJobListing>? mongoFilter = filterBuilder.Empty;

        // Filter by userId if provided
        if (!string.IsNullOrEmpty(userId))
        {
            mongoFilter &= filterBuilder.Eq(j => j.UserId, userId);
        }

        if (filters.Sites.Count != 0)
        {
            mongoFilter &= filterBuilder.In(j => j.SourceSite, filters.Sites);
        }

        if (filters.FromDate.HasValue)
        {
            mongoFilter &= filterBuilder.Gte(j => j.ScrapedAt, filters.FromDate.Value);
        }

        if (filters.ToDate.HasValue)
        {
            mongoFilter &= filterBuilder.Lte(j => j.ScrapedAt, filters.ToDate.Value);
        }

        if (filters.IsRemote.HasValue)
        {
            mongoFilter &= filterBuilder.Eq(j => j.IsRemote, filters.IsRemote.Value);
        }

        if (filters.MinMatchScore.HasValue)
        {
            mongoFilter &= filterBuilder.Gte(j => j.MatchScore, filters.MinMatchScore.Value);
        }

        if (filters.IsApplied.HasValue)
        {
            mongoFilter &= filterBuilder.Eq(j => j.IsApplied, filters.IsApplied.Value);
        }

        if (filters.RequiredSkills.Count != 0)
        {
            mongoFilter &= filterBuilder.AnyIn(j => j.RequiredSkills, filters.RequiredSkills);
        }

        return await JobListings
            .Find(mongoFilter)
            .SortByDescending(j => j.MatchScore)
            .ThenByDescending(j => j.ScrapedAt)
            .ToListAsync();
    }

    public async Task<string> TakeScreenshotAsync(string url)
    {
        try
        {
            InitializeScreenshotDriver();
            await _screenshotDriver!.Navigate().GoToUrlAsync(url);
            await Task.Delay(2000);

            Screenshot screenshot = ((ITakesScreenshot)_screenshotDriver).GetScreenshot();
            var fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine("Screenshots", fileName);
            
            Directory.CreateDirectory("Screenshots");
            screenshot.SaveAsFile(filePath);
            
            return filePath;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error taking screenshot: {ex.Message}");
            throw;
        }
    }

    private void InitializeScreenshotDriver()
    {
        if (_screenshotDriver is not null) return;

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        
        // Try to find Chrome binary path
        string[] chromePaths =
        [
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Google\Chrome\Application\chrome.exe"
        ];
        
        foreach (string path in chromePaths)
        {
            if (!File.Exists(path)) continue;
            options.BinaryLocation = path;
            logger.LogInformation($"Using Chrome binary at: {path}");
            break;
        }
        
        _screenshotDriver = new ChromeDriver(options);
    }

    private async Task CalculateMatchScores(List<EnhancedJobListing> jobs, string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            // Get user profile from MongoDB
            IMongoCollection<BsonDocument>? profileCollection = JobListings.Database.GetCollection<BsonDocument>("career_profile");
            FilterDefinition<BsonDocument>? profileFilter = Builders<BsonDocument>.Filter.Eq("userId", userId);
            BsonDocument? userProfile = await profileCollection.Find(profileFilter).FirstOrDefaultAsync();
            
            if (userProfile is null) return;

            List<string> userSkills = userProfile["experience"]["primaryTechnologies"]
                .AsBsonArray.Select(x => x.AsString.ToLower()).ToList();
            List<string> preferredSkills = userProfile["skills"]["preferred"]
                .AsBsonArray.Select(x => x.AsString.ToLower()).ToList();

            foreach (EnhancedJobListing job in jobs)
            {
                job.MatchScore = CalculateJobMatchScore(job, userSkills, preferredSkills);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Could not calculate match scores: {ex.Message}");
        }
    }

    private static double CalculateJobMatchScore(EnhancedJobListing job, List<string> userSkills, List<string> preferredSkills)
    {
        double score = 0;
        string jobText = $"{job.Title} {job.Summary} {job.FullDescription} {string.Join(" ", job.RequiredSkills)}".ToLower();

        // Base score for skill matches
        foreach (string skill in userSkills)
        {
            if (jobText.Contains(skill.ToLower()))
            {
                score += 10; // High weight for primary skills
            }
        }

        foreach (string skill in preferredSkills)
        {
            if (jobText.Contains(skill.ToLower()))
            {
                score += 5; // Medium weight for preferred skills
            }
        }

        // Bonus for remote work
        if (job.IsRemote)
        {
            score += 15;
        }

        // Penalty for low-relevance keywords
        if (jobText.Contains("junior") || jobText.Contains("entry level"))
        {
            score -= 20;
        }

        // Bonus for senior positions
        if (jobText.Contains("senior") || jobText.Contains("architect") || jobText.Contains("lead"))
        {
            score += 10;
        }

        return Math.Max(0, Math.Min(100, score)); // Normalize to 0-100
    }

    private async Task SaveJobsToDatabase(List<EnhancedJobListing> jobs)
    {
        try
        {
            var uniqueJobs = new List<EnhancedJobListing>();
            
            foreach (EnhancedJobListing job in jobs)
            {
                // Check if the job already exists (by URL)
                EnhancedJobListing? existingJob = await JobListings
                    .Find(j => j.Url == job.Url)
                    .FirstOrDefaultAsync();
                
                if (existingJob is null)
                {
                    uniqueJobs.Add(job);
                }
                else
                {
                    // Update an existing job with new scrape data
                    existingJob.ScrapedAt = DateTime.UtcNow;
                    existingJob.MatchScore = job.MatchScore;
                    
                    FilterDefinition<EnhancedJobListing>? filter = Builders<EnhancedJobListing>.Filter.Eq(j => j.Id, existingJob.Id);
                    await JobListings.ReplaceOneAsync(filter, existingJob);
                }
            }

            if (uniqueJobs.Count != 0)
            {
                await JobListings.InsertManyAsync(uniqueJobs);
                logger.LogInformation($"Saved {uniqueJobs.Count} new jobs to database");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error saving jobs to database: {ex.Message}");
        }
    }

    public async Task<bool> SaveJobsAsync(SaveJobsRequest request)
    {
        try
        {
            if (!request.Jobs.Any())
            {
                logger.LogWarning("No jobs provided to save");
                return true; // Not an error, just nothing to save
            }

            // Generate ObjectIds for jobs that don't have them
            foreach (EnhancedJobListing job in request.Jobs.Where(j => string.IsNullOrEmpty(j.Id)))
            {
                job.Id = ObjectId.GenerateNewId().ToString();
                job.ScrapedAt = DateTime.UtcNow;
            }

            await SaveJobsToDatabase(request.Jobs);
            logger.LogInformation($"Successfully saved {request.Jobs.Count} jobs for user {request.UserId}");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error saving jobs for user {request.UserId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Save jobs to temporary collection for recovery if operation is interrupted
    /// </summary>
    public async Task SaveToTemporaryCollectionAsync(
        List<EnhancedJobListing> jobs,
        string sessionId,
        int batchNumber,
        string operationType,
        string searchTerm,
        string location)
    {
        try
        {
            if (!jobs.Any())
            {
                logger.LogDebug($"No jobs to save to temporary collection for session {sessionId}");
                return;
            }

            var tempListings = jobs.Select(job => new TemporaryJobListing
            {
                SessionId = sessionId,
                BatchNumber = batchNumber,
                SavedAt = DateTime.UtcNow,
                Consolidated = false,
                JobListing = job,
                OperationType = operationType,
                SearchTerm = searchTerm,
                Location = location
            }).ToList();

            await TempJobListings.InsertManyAsync(tempListings);
            logger.LogInformation($"Saved {jobs.Count} jobs to temporary collection for session {sessionId}, batch {batchNumber}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error saving jobs to temporary collection for session {sessionId}: {ex.Message}");
            // Don't throw - temp saving should not break the main flow
        }
    }

    /// <summary>
    /// Consolidate temporary results to final collection
    /// </summary>
    public async Task<ConsolidationResult> ConsolidateTemporaryResultsAsync(string sessionId, string userId)
    {
        try
        {
            // Find all unconsolidated temp results for this session
            var tempResults = await TempJobListings
                .Find(t => t.SessionId == sessionId && !t.Consolidated)
                .SortBy(t => t.BatchNumber)
                .ToListAsync();

            if (!tempResults.Any())
            {
                logger.LogInformation($"No temporary results found for session {sessionId}");
                return new ConsolidationResult
                {
                    Success = true,
                    SessionId = sessionId,
                    JobsConsolidated = 0,
                    Message = "No temporary results to consolidate"
                };
            }

            // Extract job listings and set userId
            var jobs = tempResults.Select(t =>
            {
                t.JobListing.UserId = userId;
                return t.JobListing;
            }).ToList();

            // Save to final collection (with deduplication)
            await SaveJobsToDatabase(jobs);

            // Mark temp results as consolidated
            var filter = Builders<TemporaryJobListing>.Filter.Eq(t => t.SessionId, sessionId);
            var update = Builders<TemporaryJobListing>.Update.Set(t => t.Consolidated, true);
            await TempJobListings.UpdateManyAsync(filter, update);

            logger.LogInformation($"Consolidated {jobs.Count} jobs from session {sessionId} to final collection");

            return new ConsolidationResult
            {
                Success = true,
                SessionId = sessionId,
                JobsConsolidated = jobs.Count,
                JobsSaved = jobs.Count,
                Message = $"Successfully consolidated {jobs.Count} jobs from temporary storage"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error consolidating temporary results for session {sessionId}");
            return new ConsolidationResult
            {
                Success = false,
                SessionId = sessionId,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Get temporary results for inspection/recovery
    /// </summary>
    public async Task<List<TemporaryJobListing>> GetTemporaryResultsAsync(string? sessionId = null, bool includeConsolidated = false)
    {
        try
        {
            FilterDefinition<TemporaryJobListing> filter;

            if (!string.IsNullOrEmpty(sessionId))
            {
                filter = includeConsolidated
                    ? Builders<TemporaryJobListing>.Filter.Eq(t => t.SessionId, sessionId)
                    : Builders<TemporaryJobListing>.Filter.And(
                        Builders<TemporaryJobListing>.Filter.Eq(t => t.SessionId, sessionId),
                        Builders<TemporaryJobListing>.Filter.Eq(t => t.Consolidated, false)
                    );
            }
            else
            {
                filter = includeConsolidated
                    ? Builders<TemporaryJobListing>.Filter.Empty
                    : Builders<TemporaryJobListing>.Filter.Eq(t => t.Consolidated, false);
            }

            return await TempJobListings
                .Find(filter)
                .SortByDescending(t => t.SavedAt)
                .Limit(100)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving temporary results");
            return [];
        }
    }

    /// <summary>
    /// Clean up old temporary results (consolidated or older than 24 hours)
    /// </summary>
    public async Task<int> CleanupOldTemporaryResultsAsync()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            var filter = Builders<TemporaryJobListing>.Filter.Or(
                Builders<TemporaryJobListing>.Filter.Eq(t => t.Consolidated, true),
                Builders<TemporaryJobListing>.Filter.Lt(t => t.SavedAt, cutoffTime)
            );

            var result = await TempJobListings.DeleteManyAsync(filter);
            logger.LogInformation($"Cleaned up {result.DeletedCount} old temporary results");
            return (int)result.DeletedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up temporary results");
            return 0;
        }
    }

    public void Dispose()
    {
        _screenshotDriver?.Quit();
        _screenshotDriver?.Dispose();
    }
}