using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services;

public class EnhancedJobScrapingService(
    IJobSiteScraperFactory scraperFactory,
    IMongoDatabase database,
    ILogger<EnhancedJobScrapingService> logger)
    : IEnhancedJobScrapingService
{
    private readonly IMongoCollection<EnhancedJobListing> _jobListings = database.GetCollection<EnhancedJobListing>("search_results");
    private readonly IMongoCollection<SiteConfiguration> _siteConfigurations = database.GetCollection<SiteConfiguration>("site_configurations");
    private IWebDriver? _screenshotDriver;
    
    // Service-level semaphore to prevent concurrent scraping operations
    private static readonly SemaphoreSlim ScrapingSemaphore = new(1, 1);

    public async Task<List<EnhancedJobListing>> ScrapeMultipleSitesAsync(EnhancedScrapeRequest request)
    {
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

                tasks.Add(ScrapeSpecificSiteAsync(site, request));
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

        // Note: Jobs are not automatically saved - use SaveJobsAsync after filtering
        
        logger.LogInformation($"Successfully scraped {allJobs.Count} total jobs from {request.Sites.Count} sites");
        return allJobs.OrderByDescending(j => j.MatchScore).ToList();
    }

    public async Task<List<EnhancedJobListing>> ScrapeSpecificSiteAsync(JobSite site, EnhancedScrapeRequest request)
    {
        logger.LogInformation($"Requesting scraping lock for {site}...");
        
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
        SiteConfiguration? config = await _siteConfigurations.Find(filter).FirstOrDefaultAsync();
        
        if (config == null)
        {
            // Create default configuration using the scraper
            IJobSiteScraper scraper = scraperFactory.CreateScraper(site);
            config = scraper.GetDefaultConfiguration();
            await _siteConfigurations.InsertOneAsync(config);
            logger.LogInformation($"Created default configuration for {site}");
        }
        
        return config;
    }

    public async Task UpdateSiteConfigurationAsync(SiteConfiguration config)
    {
        config.LastUpdated = DateTime.UtcNow;
        FilterDefinition<SiteConfiguration>? filter = Builders<SiteConfiguration>.Filter.Eq(s => s.SiteName, config.SiteName);
        await _siteConfigurations.ReplaceOneAsync(filter, config, new ReplaceOptions { IsUpsert = true });
        logger.LogInformation($"Updated configuration for {config.SiteName}");
    }

    public async Task<List<EnhancedJobListing>> GetStoredJobsAsync(string userId, JobSearchFilters filters)
    {
        FilterDefinitionBuilder<EnhancedJobListing>? filterBuilder = Builders<EnhancedJobListing>.Filter;
        FilterDefinition<EnhancedJobListing>? mongoFilter = filterBuilder.Empty;

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

        return await _jobListings
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
            _screenshotDriver!.Navigate().GoToUrl(url);
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
        if (_screenshotDriver != null) return;

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
            IMongoCollection<BsonDocument>? profileCollection = _jobListings.Database.GetCollection<BsonDocument>("career_profile");
            FilterDefinition<BsonDocument>? profileFilter = Builders<BsonDocument>.Filter.Eq("userId", userId);
            BsonDocument? userProfile = await profileCollection.Find(profileFilter).FirstOrDefaultAsync();
            
            if (userProfile == null) return;

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
                EnhancedJobListing? existingJob = await _jobListings
                    .Find(j => j.Url == job.Url)
                    .FirstOrDefaultAsync();
                
                if (existingJob == null)
                {
                    uniqueJobs.Add(job);
                }
                else
                {
                    // Update an existing job with new scrape data
                    existingJob.ScrapedAt = DateTime.UtcNow;
                    existingJob.MatchScore = job.MatchScore;
                    
                    FilterDefinition<EnhancedJobListing>? filter = Builders<EnhancedJobListing>.Filter.Eq(j => j.Id, existingJob.Id);
                    await _jobListings.ReplaceOneAsync(filter, existingJob);
                }
            }

            if (uniqueJobs.Count != 0)
            {
                await _jobListings.InsertManyAsync(uniqueJobs);
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

    public void Dispose()
    {
        _screenshotDriver?.Quit();
        _screenshotDriver?.Dispose();
    }
}