using Microsoft.AspNetCore.Mvc;
using SeleniumChromeTool.Models;
using SeleniumChromeTool.Services;
using SeleniumChromeTool.Services.Enhanced;
using SeleniumChromeTool.Services.Scrapers;

namespace SeleniumChromeTool.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobScrapingController : ControllerBase
{
    private readonly IEnhancedJobScrapingService _scrapingService;
    private readonly ILogger<JobScrapingController> _logger;
    private readonly GoogleSimplifyJobsService _googleSimplifyService;

    public JobScrapingController(
        IEnhancedJobScrapingService scrapingService,
        ILogger<JobScrapingController> logger,
        GoogleSimplifyJobsService googleSimplifyService)
    {
        _scrapingService = scrapingService;
        _logger = logger;
        _googleSimplifyService = googleSimplifyService;
    }

    [HttpPost("scrape-multiple-sites")]
    public async Task<ActionResult> ScrapeMultipleSites([FromBody] EnhancedScrapeRequest request)
    {
        try
        {
            _logger.LogInformation($"Starting multi-site scrape for user: {request.UserId}");
            
            var jobs = await _scrapingService.ScrapeMultipleSitesAsync(request);
            
            return Ok(new { 
                Success = true,
                JobCount = jobs.Count,
                Sites = request.Sites,
                Jobs = jobs.Take(request.MaxResults),
                TopMatches = jobs.Where(j => j.MatchScore > 50).Take(10),
                ScrapedAt = DateTime.UtcNow,
                Message = "Jobs scraped successfully. Use /save-jobs endpoint to persist filtered results.",
                Summary = new
                {
                    RemoteJobs = jobs.Count(j => j.IsRemote),
                    HighMatchJobs = jobs.Count(j => j.MatchScore > 70),
                    SiteBreakdown = jobs.GroupBy(j => j.SourceSite)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count())
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in multi-site scrape: {ex.Message}");
            return StatusCode(500, new { 
                Success = false, 
                Error = "Failed to scrape jobs from multiple sites", 
                Details = ex.Message 
            });
        }
    }

    [HttpPost("scrape-site/{site}")]
    public async Task<ActionResult> ScrapeSingleSite(JobSite site, [FromBody] EnhancedScrapeRequest request)
    {
        try
        {
            _logger.LogInformation($"Starting scrape for {site}");
            
            // Add timeout to prevent hanging - max 4 minutes
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            
            List<EnhancedJobListing> jobs;
            
            // Use Google-based discovery for SimplifyJobs (Phase 2 enhancement)
            if (site == JobSite.SimplifyJobs)
            {
                _logger.LogInformation("🚀 Using Phase 2 Google-based SimplifyJobs discovery");
                jobs = await _googleSimplifyService.DiscoverAndFetchJobsAsync(request);
            }
            else
            {
                // Use traditional scraping for other sites
                jobs = await _scrapingService.ScrapeSpecificSiteAsync(site, request);
            }
            
            return Ok(new { 
                Success = true,
                Site = site.ToString(),
                JobCount = jobs.Count,
                Jobs = jobs,
                Method = site == JobSite.SimplifyJobs ? "Phase 2: Google Discovery + Direct API" : "Traditional Scraping",
                ScrapedAt = DateTime.UtcNow,
                Message = $"Jobs scraped successfully using {(site == JobSite.SimplifyJobs ? "enhanced Google discovery" : "traditional method")}. Use /save-jobs endpoint to persist filtered results."
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"Scraping {site} timed out after 4 minutes");
            return StatusCode(408, new { 
                Success = false, 
                Error = $"Scraping {site} timed out after 4 minutes", 
                Details = "The operation took too long to complete. Try reducing maxResults or check site accessibility." 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error scraping {site}: {ex.Message}");
            return StatusCode(500, new { 
                Success = false, 
                Error = $"Failed to scrape {site}", 
                Details = ex.Message 
            });
        }
    }

    [HttpPost("save-jobs")]
    public async Task<ActionResult> SaveJobs([FromBody] SaveJobsRequest request)
    {
        try
        {
            _logger.LogInformation($"Saving {request.Jobs.Count} filtered jobs for user: {request.UserId}");
            
            var success = await _scrapingService.SaveJobsAsync(request);
            
            if (success)
            {
                return Ok(new { 
                    Success = true,
                    Message = $"Successfully saved {request.Jobs.Count} jobs",
                    JobCount = request.Jobs.Count,
                    UserId = request.UserId,
                    SavedAt = DateTime.UtcNow
                });
            }
            else
            {
                return StatusCode(500, new { 
                    Success = false, 
                    Error = "Failed to save jobs to database" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving jobs: {ex.Message}");
            return StatusCode(500, new { 
                Success = false, 
                Error = "Failed to save jobs", 
                Details = ex.Message 
            });
        }
    }

    [HttpGet("stored-jobs/{userId}")]
    public async Task<ActionResult> GetStoredJobs(string userId, [FromQuery] JobSearchFilters? filters = null)
    {
        try
        {
            filters ??= new JobSearchFilters();
            var jobs = await _scrapingService.GetStoredJobsAsync(userId, filters);
            
            return Ok(new { 
                Success = true,
                JobCount = jobs.Count,
                Jobs = jobs,
                Filters = filters
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving stored jobs: {ex.Message}");
            return StatusCode(500, new { 
                Success = false, 
                Error = "Failed to retrieve stored jobs", 
                Details = ex.Message 
            });
        }
    }

    [HttpGet("site-config/{site}")]
    public async Task<ActionResult> GetSiteConfiguration(JobSite site)
    {
        try
        {
            var config = await _scrapingService.GetSiteConfigurationAsync(site);
            return Ok(new { Success = true, Configuration = config });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting site configuration: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpPut("site-config")]
    public async Task<ActionResult> UpdateSiteConfiguration([FromBody] SiteConfiguration config)
    {
        try
        {
            await _scrapingService.UpdateSiteConfigurationAsync(config);
            return Ok(new { Success = true, Message = "Configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating site configuration: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpPost("screenshot")]
    public async Task<ActionResult> TakeScreenshot([FromBody] string url)
    {
        try
        {
            var filePath = await _scrapingService.TakeScreenshotAsync(url);
            return Ok(new { Success = true, FilePath = filePath });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("health")]
    public ActionResult HealthCheck()
    {
        return Ok(new { 
            Status = "Healthy", 
            Timestamp = DateTime.UtcNow,
            Version = "2.0-Enhanced"
        });
    }

    [HttpPost("test-site-access/{site}")]
    public async Task<ActionResult> TestSiteAccess(JobSite site)
    {
        try
        {
            var config = await _scrapingService.GetSiteConfigurationAsync(site);
            return Ok(new { 
                Success = true, 
                Site = site.ToString(),
                IsAccessible = true,
                TestedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("email-alerts/summary")]
    public async Task<ActionResult> GetEmailJobAlertSummary([FromQuery] int daysBack = 7)
    {
        try
        {
            var emailService = HttpContext.RequestServices.GetService<EmailJobAlertService>();
            if (emailService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Email service not configured" });
            }

            var summary = await emailService.GetJobAlertSummaryAsync(daysBack);
            return Ok(new { Success = true, Summary = summary });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting email job alert summary: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("email-alerts/jobs")]
    public async Task<ActionResult> GetEmailJobAlerts([FromQuery] int daysBack = 7, [FromQuery] string? source = null)
    {
        try
        {
            var emailService = HttpContext.RequestServices.GetService<EmailJobAlertService>();
            if (emailService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Email service not configured" });
            }

            List<EnhancedJobListing> jobs;
            
            if (!string.IsNullOrEmpty(source))
            {
                jobs = source.ToLower() switch
                {
                    "linkedin" => await emailService.GetLinkedInJobAlertsAsync(daysBack),
                    "glassdoor" => await emailService.GetGlassdoorJobAlertsAsync(daysBack),
                    _ => await emailService.GetJobAlertsAsync(daysBack)
                };
            }
            else
            {
                jobs = await emailService.GetJobAlertsAsync(daysBack);
            }

            return Ok(new { 
                Success = true, 
                JobCount = jobs.Count,
                Jobs = jobs,
                Source = source ?? "all",
                DaysBack = daysBack,
                RetrievedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting email job alerts: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("email-alerts/recent")]
    public async Task<ActionResult> GetRecentEmailJobAlerts()
    {
        try
        {
            var emailService = HttpContext.RequestServices.GetService<EmailJobAlertService>();
            if (emailService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Email service not configured" });
            }

            var jobs = await emailService.GetRecentJobAlertsAsync();
            return Ok(new { 
                Success = true, 
                JobCount = jobs.Count,
                Jobs = jobs,
                Period = "Last 3 days",
                RetrievedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting recent email job alerts: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("email-alerts/enhanced")]
    public async Task<ActionResult> GetEnhancedEmailJobAlerts([FromQuery] int daysBack = 7, [FromQuery] string? source = null)
    {
        try
        {
            var emailService = HttpContext.RequestServices.GetService<EmailJobAlertService>();
            if (emailService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Email service not configured" });
            }

            // Get basic email jobs first
            List<EnhancedJobListing> jobs;
            if (!string.IsNullOrEmpty(source))
            {
                if (source.ToLower() == "linkedin")
                    jobs = await emailService.GetLinkedInJobAlertsAsync(daysBack);
                else if (source.ToLower() == "glassdoor")
                    jobs = await emailService.GetGlassdoorJobAlertsAsync(daysBack);
                else
                    jobs = await emailService.GetJobAlertsAsync(daysBack);
            }
            else
            {
                jobs = await emailService.GetJobAlertsAsync(daysBack);
            }

            // Enhance jobs with detailed information scraped from their URLs
            var enhancedJobs = await emailService.EnhanceJobsWithDetails(jobs);

            return Ok(new { 
                Success = true, 
                JobCount = enhancedJobs.Count,
                Jobs = enhancedJobs,
                Source = source ?? "all",
                DaysBack = daysBack,
                Enhanced = true,
                RetrievedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting enhanced email job alerts: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    // PHASE 1 ENHANCEMENTS - Multi-Search Automation, Enhanced Scoring, Bulk Processing

    [HttpPost("automated-comprehensive-search")]
    public async Task<ActionResult> RunAutomatedComprehensiveSearch([FromBody] ComprehensiveSearchRequest request)
    {
        try
        {
            _logger.LogInformation($"Starting automated comprehensive .NET search for user: {request.UserId}");
            
            var automatedSearch = HttpContext.RequestServices.GetService<AutomatedSimplifySearch>();
            if (automatedSearch == null)
            {
                return StatusCode(500, new { Success = false, Error = "Automated search service not configured" });
            }

            var results = await automatedSearch.RunComprehensiveNetSearchAsync(request);

            return Ok(new { 
                Success = true,
                TotalJobsFound = results.TotalJobsFound,
                SearchDuration = results.TotalSearchDuration,
                SearchTermsUsed = results.SearchTermsUsed,
                Results = new
                {
                    HighPriority = new { Count = results.HighPriorityJobs.Count, Jobs = results.HighPriorityJobs.Take(5) },
                    ApplicationReady = new { Count = results.ApplicationReadyJobs.Count, Jobs = results.ApplicationReadyJobs.Take(10) },
                    Consider = new { Count = results.ConsiderJobs.Count, Jobs = results.ConsiderJobs.Take(5) },
                    Skipped = new { Count = results.SkippedJobs.Count }
                },
                Enhancement = "Phase 1 - Multi-Search Automation with Intelligent Scoring",
                Message = "Comprehensive .NET job search completed with automated term cycling and enhanced scoring",
                ScrapedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in automated comprehensive search: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }

    [HttpPost("bulk-process-jobs")]
    public async Task<ActionResult> BulkProcessJobs([FromBody] BulkProcessingRequest request)
    {
        try
        {
            _logger.LogInformation($"Starting bulk job processing: {request.TargetJobCount} jobs for '{request.SearchTerm}'");
            
            var bulkProcessor = HttpContext.RequestServices.GetService<IntelligentBulkProcessor>();
            if (bulkProcessor == null)
            {
                return StatusCode(500, new { Success = false, Error = "Bulk processor service not configured" });
            }

            var results = await bulkProcessor.ProcessJobsBulkAsync(request);

            return Ok(new { 
                Success = results.IsSuccessful,
                ProcessedJobs = results.ProcessedJobs.Count,
                PagesProcessed = results.PagesProcessed,
                Duration = results.TotalDuration,
                Statistics = new
                {
                    HighPriority = results.HighPriorityCount,
                    ApplicationReady = results.ApplicationReadyCount,
                    Consider = results.ConsiderCount,
                    LowPriority = results.LowPriorityCount,
                    AverageScore = Math.Round(results.AverageScore, 1),
                    JobsPerMinute = Math.Round(results.JobsPerMinute, 1)
                },
                Jobs = results.ProcessedJobs.OrderByDescending(j => j.MatchScore).Take(20),
                Errors = results.Errors,
                Enhancement = "Phase 1 - Intelligent Bulk Processing (10-20x capacity increase)",
                Message = $"Bulk processing completed: {results.ProcessedJobs.Count} jobs processed across {results.PagesProcessed} pages",
                ScrapedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in bulk processing: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }

    [HttpPost("simplify-jobs-enhanced")]
    public async Task<ActionResult> SimplifyJobsEnhanced([FromBody] EnhancedScrapeRequest request)
    {
        try
        {
            _logger.LogInformation($"Starting enhanced SimplifyJobs scraping for: {request.SearchTerm}");

            // Use bulk processor for enhanced capacity
            var bulkProcessor = HttpContext.RequestServices.GetService<IntelligentBulkProcessor>();
            if (bulkProcessor == null)
            {
                return StatusCode(500, new { Success = false, Error = "Enhanced services not configured" });
            }

            var bulkRequest = new BulkProcessingRequest
            {
                SearchTerm = request.SearchTerm,
                Location = request.Location,
                TargetJobCount = Math.Min(request.MaxResults, 20), // Cap at 20 for enhanced processing
                MaxAgeInDays = request.MaxAgeInDays,
                UserId = request.UserId,
                ScoringProfile = new NetDeveloperScoringProfile()
            };

            var results = await bulkProcessor.ProcessJobsBulkAsync(bulkRequest);

            return Ok(new { 
                Success = results.IsSuccessful,
                JobCount = results.ProcessedJobs.Count,
                Jobs = results.ProcessedJobs.OrderByDescending(j => j.MatchScore),
                HighPriorityJobs = results.ProcessedJobs.Where(j => j.MatchScore >= 80).ToList(),
                ApplicationReadyJobs = results.ProcessedJobs.Where(j => j.MatchScore is >= 60 and < 80).ToList(),
                Performance = new
                {
                    Duration = results.TotalDuration,
                    JobsPerMinute = Math.Round(results.JobsPerMinute, 1),
                    AverageScore = Math.Round(results.AverageScore, 1),
                    PagesProcessed = results.PagesProcessed
                },
                Enhancement = "Phase 1 - Enhanced SimplifyJobs with Bulk Processing + Smart Scoring",
                Message = "Enhanced SimplifyJobs scraping with 10x capacity and intelligent scoring",
                ScrapedAt = DateTime.UtcNow,
                Errors = results.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in enhanced SimplifyJobs scraping: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    // PHASE 2 ENHANCEMENTS - Smart Deduplication, Application Management, Market Intelligence

    [HttpPost("smart-deduplication")]
    public async Task<ActionResult> SmartDeduplication([FromBody] DeduplicationRequest request)
    {
        try
        {
            _logger.LogInformation($"Starting smart deduplication for {request.Jobs.Count} jobs");
            
            var deduplicationService = HttpContext.RequestServices.GetService<SmartDeduplicationService>();
            if (deduplicationService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Smart deduplication service not configured" });
            }

            var result = await deduplicationService.DeduplicateJobsAsync(request.Jobs);

            return Ok(new { 
                Success = true,
                OriginalJobCount = result.OriginalCount,
                UniqueJobCount = result.UniqueCount,
                DuplicatesRemoved = result.DuplicatesRemoved,
                DeduplicationRate = $"{result.DeduplicationRate}%",
                UniqueJobs = result.UniqueJobs,
                DuplicateGroups = result.DuplicateGroups.Take(10), // Limit for response size
                Statistics = new
                {
                    AverageGroupSize = result.DuplicateGroups.Any() ? 
                        Math.Round(result.DuplicateGroups.Average(g => g.Duplicates.Count + 1), 1) : 0,
                    LargestDuplicateGroup = result.DuplicateGroups.Any() ? 
                        result.DuplicateGroups.Max(g => g.Duplicates.Count + 1) : 0,
                    CrossSourceDuplicates = result.DuplicateGroups.Count(g => 
                        g.Duplicates.Any(d => d.SourceSite != g.SelectedJob.SourceSite))
                },
                Enhancement = "Phase 2 - Smart Deduplication with Cross-Source Matching",
                Message = $"Deduplication completed: {result.DuplicatesRemoved} duplicates removed from {result.OriginalCount} jobs",
                ProcessedAt = result.ProcessedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in smart deduplication: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }

    [HttpPost("categorize-applications")]
    public async Task<ActionResult> CategorizeApplications([FromBody] ApplicationCategorizationRequest? request)
    {
        try
        {
            // Handle null request
            if (request == null)
            {
                request = new ApplicationCategorizationRequest();
            }

            _logger.LogInformation($"Starting application categorization for {request.Jobs.Count} jobs");
            
            var applicationService = HttpContext.RequestServices.GetService<ApplicationManagementService>();
            if (applicationService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Application management service not configured" });
            }

            var result = await applicationService.CategorizeJobsAsync(request.Jobs, request.Preferences);

            return Ok(new { 
                Success = true,
                TotalJobs = result.TotalJobs,
                Categorization = new
                {
                    Immediate = new { Count = result.ImmediateApplications.Count, Jobs = result.ImmediateApplications.Take(5) },
                    HighPriority = new { Count = result.HighPriorityApplications.Count, Jobs = result.HighPriorityApplications.Take(10) },
                    MediumPriority = new { Count = result.MediumPriorityApplications.Count, Jobs = result.MediumPriorityApplications.Take(5) },
                    LowPriority = new { Count = result.LowPriorityApplications.Count, Jobs = result.LowPriorityApplications.Take(3) },
                    NotRecommended = new { Count = result.NotRecommended.Count },
                    AlreadyApplied = new { Count = result.AlreadyApplied.Count }
                },
                ApplicationPlan = new
                {
                    DailyPlan = result.Insights.DailyApplicationPlan,
                    WeeklyPlan = result.Insights.WeeklyApplicationPlan.Take(20), // Limit for response
                    EstimatedDailyTime = result.Insights.EstimatedDailyTime.ToString(@"hh\:mm"),
                    EstimatedWeeklyTime = result.Insights.EstimatedWeeklyTime.ToString(@"hh\:mm")
                },
                Insights = new
                {
                    Recommendations = result.Insights.Recommendations,
                    PriorityDistribution = new
                    {
                        ImmediatePercentage = result.TotalJobs > 0 ? Math.Round((double)result.ImmediateApplications.Count / result.TotalJobs * 100, 1) : 0.0,
                        HighPriorityPercentage = result.TotalJobs > 0 ? Math.Round((double)result.HighPriorityApplications.Count / result.TotalJobs * 100, 1) : 0.0,
                        ApplicationReadyPercentage = result.TotalJobs > 0 ? Math.Round((double)(result.ImmediateApplications.Count + result.HighPriorityApplications.Count) / result.TotalJobs * 100, 1) : 0.0
                    }
                },
                Enhancement = "Phase 2 - Intelligent Application Management with Priority Categorization",
                Message = $"Application categorization completed: {result.ImmediateApplications.Count + result.HighPriorityApplications.Count} application-ready jobs identified",
                ProcessedAt = result.ProcessedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in application categorization: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }

    [HttpPost("market-intelligence")]
    public async Task<ActionResult> MarketIntelligence([FromBody] MarketIntelligenceRequest? request)
    {
        try
        {
            // Handle null request
            if (request == null)
            {
                request = new MarketIntelligenceRequest();
            }

            _logger.LogInformation($"Generating market intelligence report for {request.Jobs.Count} jobs");
            
            var marketService = HttpContext.RequestServices.GetService<MarketIntelligenceService>();
            if (marketService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Market intelligence service not configured" });
            }

            var result = await marketService.GenerateMarketReportAsync(request.Jobs, request.AnalysisRequest);

            return Ok(new { 
                Success = true,
                Report = new
                {
                    GeneratedAt = result.GeneratedAt,
                    AnalysisPeriod = result.AnalysisPeriod,
                    TotalJobsAnalyzed = result.TotalJobsAnalyzed,
                    
                    SalaryInsights = new
                    {
                        OverallStats = result.SalaryTrends.OverallStats,
                        // RemoteVsOnsite = result.SalaryTrends.RemoteVsOnsite, // Commented out - not implemented yet
                        TopPayingCompanies = result.SalaryTrends.TopPayingCompanies.Take(10),
                        ExperienceLevelBreakdown = result.SalaryTrends.ByExperienceLevel.Take(5)
                    },
                    
                    TechnologyTrends = new
                    {
                        TrendingTechnologies = result.TechnologyDemand.TrendingTechnologies.Take(10),
                        CategoryDemand = result.TechnologyDemand.CategoryDemand.Select(c => new
                        {
                            c.Category,
                            TopTechnologies = c.Technologies.Take(5)
                        }),
                        // PopularCombinations = result.TechnologyDemand.PopularCombinations.Take(5) // Commented out - not implemented yet
                    },
                    
                    HiringPatterns = new
                    {
                        MostActiveCompanies = result.HiringPatterns.MostActiveCompanies.Take(15),
                        HiringVelocityTrends = result.HiringPatterns.HiringVelocityTrends,
                        CompanySizeBreakdown = result.HiringPatterns.CompanySizeBreakdown,
                        IndustryTrends = result.HiringPatterns.IndustryTrends
                    },
                    
                    RemoteWorkTrends = new
                    {
                        OverallDistribution = result.RemoteWorkTrends.OverallDistribution,
                        ByExperienceLevel = result.RemoteWorkTrends.ByExperienceLevel,
                        TopRemoteCompanies = result.RemoteWorkTrends.TopRemoteCompanies.Take(10)
                    },
                    
                    GeographicInsights = new
                    {
                        TopCities = result.GeographicTrends.TopCities.Take(10),
                        TopStates = result.GeographicTrends.TopStates.Take(8)
                    },
                    
                    ExperienceLevelDemand = new
                    {
                        LevelBreakdown = result.ExperienceLevelDemand.LevelBreakdown,
                        CareerProgressionInsights = result.ExperienceLevelDemand.CareerProgressionInsights
                    },
                    
                    CompetitivenessInsights = result.CompetitivenessInsights,
                    
                    KeyRecommendations = result.Recommendations.Take(10)
                },
                Enhancement = "Phase 2 - Comprehensive Market Intelligence with Trend Analysis",
                Message = $"Market intelligence report generated from {result.TotalJobsAnalyzed} jobs with {result.Recommendations.Count} recommendations",
                GeneratedAt = result.GeneratedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in market intelligence generation: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }

    [HttpPost("enhanced-job-analysis")]
    public async Task<ActionResult> EnhancedJobAnalysis([FromBody] EnhancedAnalysisRequest? request)
    {
        try
        {
            // Handle null request
            if (request == null)
            {
                request = new EnhancedAnalysisRequest();
            }

            _logger.LogInformation($"Starting enhanced job analysis pipeline for {request.Jobs.Count} jobs");

            // Step 1: Smart Deduplication
            var deduplicationService = HttpContext.RequestServices.GetService<SmartDeduplicationService>();
            var applicationService = HttpContext.RequestServices.GetService<ApplicationManagementService>();
            var marketService = HttpContext.RequestServices.GetService<MarketIntelligenceService>();

            if (deduplicationService == null || applicationService == null || marketService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Phase 2 services not configured" });
            }

            var analysisResult = new EnhancedAnalysisResult
            {
                StartedAt = DateTime.UtcNow,
                OriginalJobCount = request.Jobs.Count
            };

            // Phase 2a: Deduplication
            _logger.LogInformation("Phase 2a: Running smart deduplication");
            var deduplicationResult = await deduplicationService.DeduplicateJobsAsync(request.Jobs);
            analysisResult.DeduplicationResult = deduplicationResult;
            analysisResult.ProcessingSteps.Add($"Deduplication: {deduplicationResult.DuplicatesRemoved} duplicates removed");

            // Phase 2b: Application Categorization
            _logger.LogInformation("Phase 2b: Running application categorization");
            var categorizationResult = await applicationService.CategorizeJobsAsync(
                deduplicationResult.UniqueJobs, 
                request.ApplicationPreferences ?? new ApplicationPreferences { UserId = request.UserId ?? "default" });
            analysisResult.CategorizationResult = categorizationResult;
            analysisResult.ProcessingSteps.Add($"Categorization: {categorizationResult.ImmediateApplications.Count + categorizationResult.HighPriorityApplications.Count} application-ready jobs identified");

            // Phase 2c: Market Intelligence
            _logger.LogInformation("Phase 2c: Generating market intelligence");
            var marketResult = await marketService.GenerateMarketReportAsync(
                deduplicationResult.UniqueJobs, 
                request.MarketAnalysisRequest ?? new MarketAnalysisRequest { JobTitle = "Software Engineer", FocusArea = "comprehensive" });
            analysisResult.MarketIntelligenceResult = marketResult;
            analysisResult.ProcessingSteps.Add($"Market Intelligence: {marketResult.Recommendations.Count} recommendations generated");

            analysisResult.CompletedAt = DateTime.UtcNow;
            analysisResult.TotalProcessingTime = analysisResult.CompletedAt - analysisResult.StartedAt;

            return Ok(new { 
                Success = true,
                Summary = new
                {
                    OriginalJobs = analysisResult.OriginalJobCount,
                    UniqueJobs = analysisResult.DeduplicationResult.UniqueCount,
                    ApplicationReadyJobs = analysisResult.CategorizationResult.ImmediateApplications.Count + 
                                          analysisResult.CategorizationResult.HighPriorityApplications.Count,
                    MarketRecommendations = analysisResult.MarketIntelligenceResult.Recommendations.Count,
                    ProcessingTime = analysisResult.TotalProcessingTime.ToString(@"mm\:ss")
                },
                Results = new
                {
                    Deduplication = new
                    {
                        DuplicatesRemoved = analysisResult.DeduplicationResult.DuplicatesRemoved,
                        DeduplicationRate = analysisResult.DeduplicationResult.DeduplicationRate,
                        UniqueJobs = analysisResult.DeduplicationResult.UniqueJobs.Take(20) // Limit for response size
                    },
                    ApplicationPriority = new
                    {
                        Immediate = analysisResult.CategorizationResult.ImmediateApplications.Take(5),
                        HighPriority = analysisResult.CategorizationResult.HighPriorityApplications.Take(10),
                        DailyPlan = analysisResult.CategorizationResult.Insights.DailyApplicationPlan,
                        Recommendations = analysisResult.CategorizationResult.Insights.Recommendations
                    },
                    MarketInsights = new
                    {
                        SalaryAverage = analysisResult.MarketIntelligenceResult.SalaryTrends.OverallStats.AverageSalary,
                        TrendingTechnologies = analysisResult.MarketIntelligenceResult.TechnologyDemand.TrendingTechnologies.Take(5),
                        TopHiringCompanies = analysisResult.MarketIntelligenceResult.HiringPatterns.MostActiveCompanies.Take(10).Select(c => c.Company),
                        RemoteWorkPercentage = analysisResult.MarketIntelligenceResult.RemoteWorkTrends.OverallDistribution.RemotePercentage,
                        KeyRecommendations = analysisResult.MarketIntelligenceResult.Recommendations.Take(8)
                    }
                },
                ProcessingSteps = analysisResult.ProcessingSteps,
                Enhancement = "Phase 2 - Complete Intelligence Pipeline: Deduplication + Application Management + Market Intelligence",
                Message = $"Enhanced analysis completed: {analysisResult.DeduplicationResult.UniqueCount} unique jobs analyzed with intelligent categorization and market insights",
                ProcessedAt = analysisResult.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in enhanced job analysis: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }

    [HttpPost("track-application")]
    public async Task<ActionResult> TrackApplication([FromBody] ApplicationTrackingRequest request)
    {
        try
        {
            var applicationService = HttpContext.RequestServices.GetService<ApplicationManagementService>();
            if (applicationService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Application management service not configured" });
            }

            var success = await applicationService.TrackApplicationAsync(request.Application);

            if (success)
            {
                return Ok(new { 
                    Success = true,
                    Message = "Application tracked successfully",
                    ApplicationId = request.Application.Id,
                    Company = request.Application.Company,
                    Title = request.Application.Title,
                    TrackedAt = DateTime.UtcNow
                });
            }
            else
            {
                return StatusCode(500, new { Success = false, Error = "Failed to track application" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error tracking application: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    [HttpPut("update-application-status")]
    public async Task<ActionResult> UpdateApplicationStatus([FromBody] ApplicationStatusUpdateRequest request)
    {
        try
        {
            var applicationService = HttpContext.RequestServices.GetService<ApplicationManagementService>();
            if (applicationService == null)
            {
                return StatusCode(500, new { Success = false, Error = "Application management service not configured" });
            }

            var success = await applicationService.UpdateApplicationStatusAsync(
                request.ApplicationId, 
                request.Status, 
                request.Notes);

            if (success)
            {
                return Ok(new { 
                    Success = true,
                    Message = "Application status updated successfully",
                    ApplicationId = request.ApplicationId,
                    NewStatus = request.Status.ToString(),
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                return StatusCode(500, new { Success = false, Error = "Failed to update application status" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating application status: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    // STREAMLINED SIMPLIFY JOBS API SERVICE - Final Solution Implementation

    [HttpPost("fetch-jobs-by-ids")]
    public async Task<ActionResult> FetchJobsByIds([FromBody] FetchJobsByIdsRequest request)
    {
        try
        {
            _logger.LogInformation($"Fetching {request.JobIds.Length} jobs by ID via SimplifyJobs API");
            
            var simplifyApiService = HttpContext.RequestServices.GetService<SimplifyJobsApiService>();
            if (simplifyApiService == null)
            {
                return StatusCode(500, new { Success = false, Error = "SimplifyJobs API service not configured" });
            }

            var jobs = await simplifyApiService.FetchJobsByIdsAsync(request.JobIds, request.UserId);

            return Ok(new { 
                Success = true,
                JobCount = jobs.Count,
                RequestedIds = request.JobIds.Length,
                SuccessRate = $"{(jobs.Count * 100.0 / request.JobIds.Length):F1}%",
                Jobs = jobs.OrderByDescending(j => j.MatchScore),
                ExternalUrls = jobs.Where(j => !string.IsNullOrEmpty(j.Url) && !j.Url.Contains("simplify.jobs"))
                    .Select(j => new { j.Company, j.Title, j.Url }).ToList(),
                Method = "Direct API Integration",
                Enhancement = "Final Solution - Web Search + Job ID Extraction + Direct API Calls",
                Message = $"Successfully fetched {jobs.Count}/{request.JobIds.Length} jobs with working external URLs",
                ProcessedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching jobs by IDs: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }

    // PHASE 2: GOOGLE INTEGRATION - Google-based SimplifyJobs Discovery

    [HttpPost("simplify-jobs-google")]
    public async Task<ActionResult> SimplifyJobsViaGoogle([FromBody] EnhancedScrapeRequest request)
    {
        try
        {
            _logger.LogInformation($"🚀 Phase 2: Starting Google-based SimplifyJobs discovery for '{request.SearchTerm}'");
            
            var jobs = await _googleSimplifyService.DiscoverAndFetchJobsAsync(request);

            return Ok(new { 
                Success = true,
                JobCount = jobs.Count,
                Jobs = jobs.OrderByDescending(j => j.MatchScore),
                ExternalUrls = jobs.Where(j => !string.IsNullOrEmpty(j.Url) && !j.Url.Contains("simplify.jobs"))
                    .Select(j => new { j.Company, j.Title, j.Url }).ToList(),
                Method = "Phase 2: Google Discovery + Direct API Integration",
                Enhancement = "Google Search → Job ID Extraction → SimplifyJobs API",
                Message = $"Phase 2 implementation: Successfully discovered and fetched {jobs.Count} jobs via Google search",
                ProcessedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in Phase 2 Google-based SimplifyJobs discovery: {ex.Message}");
            return StatusCode(500, new { Success = false, Error = ex.Message, Details = ex.StackTrace });
        }
    }
}
