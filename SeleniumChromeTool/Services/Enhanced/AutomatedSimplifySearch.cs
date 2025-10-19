using SeleniumChromeTool.Models;
using SeleniumChromeTool.Services.Scrapers;

namespace SeleniumChromeTool.Services.Enhanced;

/// <summary>
/// Phase 1 Enhancement: Multi-Search Automation for SimplifyJobs
/// Automatically cycles through .NET-focused search terms to find 10x more relevant jobs
/// </summary>
public class AutomatedSimplifySearch
{
    private readonly ILogger<AutomatedSimplifySearch> _logger;
    private readonly SimplifyJobsScraper _scraper;
    private readonly NetDeveloperJobScorer _scorer;

    public AutomatedSimplifySearch(
        ILogger<AutomatedSimplifySearch> logger,
        SimplifyJobsScraper scraper,
        NetDeveloperJobScorer scorer)
    {
        _logger = logger;
        _scraper = scraper;
        _scorer = scorer;
    }

    /// <summary>
    /// Runs comprehensive .NET job search across multiple search terms automatically
    /// </summary>
    public async Task<EnhancedSearchResults> RunComprehensiveNetSearchAsync(ComprehensiveSearchRequest request)
    {
        var results = new EnhancedSearchResults
        {
            SearchStartTime = DateTime.UtcNow,
            SearchTermsUsed = [],
            TotalJobsFound = 0,
            HighPriorityJobs = [],
            ApplicationReadyJobs = [],
            ConsiderJobs = [],
            SkippedJobs = []
        };

        try
        {
            _logger.LogInformation("Starting comprehensive .NET job search with automation");

            List<string> searchTerms = GetNetFocusedSearchTerms(request);
            List<string> locations = GetTargetLocations(request);
            List<string> experienceLevels = GetExperienceLevels(request);

            _logger.LogInformation($"Will search across {searchTerms.Count} terms, {locations.Count} locations, {experienceLevels.Count} experience levels");

            SiteConfiguration config = _scraper.GetDefaultConfiguration();
            
            // Track consecutive low-scoring jobs to determine when to stop
            var consecutiveLowScoreJobs = 0;
            const int maxConsecutiveLowScore = 3;

            foreach (string searchTerm in searchTerms)
            {
                foreach (string location in locations)
                {
                    foreach (string experienceLevel in experienceLevels)
                    {
                        if (results.TotalJobsFound >= request.MaxTotalResults)
                        {
                            _logger.LogInformation($"Reached maximum results limit: {request.MaxTotalResults}");
                            break;
                        }

                        if (consecutiveLowScoreJobs >= maxConsecutiveLowScore)
                        {
                            _logger.LogInformation($"Stopping search: {consecutiveLowScoreJobs} consecutive low-scoring jobs found");
                            break;
                        }

                        var searchRequest = new EnhancedScrapeRequest
                        {
                            SearchTerm = searchTerm,
                            Location = location,
                            MaxResults = request.JobsPerSearch,
                            IncludeDescription = true,
                            MaxAgeInDays = request.MaxAgeInDays,
                            UserId = request.UserId
                        };

                        _logger.LogInformation($"Searching: '{searchTerm}' in '{location}' for '{experienceLevel}' level");
                        results.SearchTermsUsed.Add($"{searchTerm} | {location} | {experienceLevel}");

                        try
                        {
                            List<EnhancedJobListing> jobs = await _scraper.ScrapeJobsAsync(searchRequest, config);
                            _logger.LogInformation($"Found {jobs.Count} jobs for search: {searchTerm}");

                            var foundHighScoringJob = false;

                            foreach (EnhancedJobListing job in jobs)
                            {
                                // Enhanced scoring using NetDeveloperJobScorer
                                JobScoringResult scoringResult = _scorer.CalculateEnhancedMatchScore(job, request.ScoringProfile);
                                job.MatchScore = scoringResult.TotalScore;

                                // Store detailed scoring info in job notes
                                job.Notes = (job.Notes ?? "") + $" | Score: {scoringResult.TotalScore}% " +
                                           $"(Salary: {scoringResult.SalaryScore}, Tech: {scoringResult.TechnologyScore}, " +
                                           $"Company: {scoringResult.CompanyScore}, Remote: {scoringResult.RemoteScore}, " +
                                           $"Experience: {scoringResult.ExperienceScore})";

                                // Categorize based on score thresholds
                                if (scoringResult.TotalScore >= 80)
                                {
                                    results.HighPriorityJobs.Add(job);
                                    foundHighScoringJob = true;
                                    _logger.LogInformation($"High priority job found: {job.Title} at {job.Company} (Score: {scoringResult.TotalScore}%)");
                                }
                                else if (scoringResult.TotalScore >= 60)
                                {
                                    results.ApplicationReadyJobs.Add(job);
                                    foundHighScoringJob = true;
                                    _logger.LogInformation($"Application ready job found: {job.Title} at {job.Company} (Score: {scoringResult.TotalScore}%)");
                                }
                                else if (scoringResult.TotalScore >= 40)
                                {
                                    results.ConsiderJobs.Add(job);
                                }
                                else
                                {
                                    results.SkippedJobs.Add(job);
                                }

                                results.TotalJobsFound++;
                            }

                            // Update consecutive low score counter
                            if (foundHighScoringJob)
                            {
                                consecutiveLowScoreJobs = 0;
                            }
                            else
                            {
                                consecutiveLowScoreJobs++;
                            }

                            // Rate limiting between search iterations
                            await Task.Delay(1000);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Search failed for '{searchTerm}' in '{location}': {ex.Message}");
                        }
                    }
                }
            }

            results.SearchEndTime = DateTime.UtcNow;
            results.TotalSearchDuration = results.SearchEndTime - results.SearchStartTime;

            _logger.LogInformation("Comprehensive search completed:");
            _logger.LogInformation($"  Total Jobs Found: {results.TotalJobsFound}");
            _logger.LogInformation($"  High Priority (80%+): {results.HighPriorityJobs.Count}");
            _logger.LogInformation($"  Application Ready (60%+): {results.ApplicationReadyJobs.Count}");
            _logger.LogInformation($"  Consider (40%+): {results.ConsiderJobs.Count}");
            _logger.LogInformation($"  Search Duration: {results.TotalSearchDuration}");

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in comprehensive search: {ex.Message}");
            throw;
        }
    }

    private List<string> GetNetFocusedSearchTerms(ComprehensiveSearchRequest request)
    {
        var searchTerms = new List<string>();

        if (request.CustomSearchTerms?.Any() == true)
        {
            searchTerms.AddRange(request.CustomSearchTerms);
        }
        else
        {
            // Default .NET-focused search terms based on analysis
            searchTerms.AddRange([
                "Senior .NET Developer",
                "Principal Software Engineer .NET",
                "Staff Engineer C#",
                ".NET Architect", 
                "Backend Developer .NET",
                "Full Stack .NET Developer",
                "Senior C# Developer",
                "Lead .NET Engineer",
                ".NET Core Developer",
                "Senior Backend Engineer"
            ]);
        }

        return searchTerms;
    }

    private List<string> GetTargetLocations(ComprehensiveSearchRequest request)
    {
        var locations = new List<string>();

        if (request.CustomLocations?.Any() == true)
        {
            locations.AddRange(request.CustomLocations);
        }
        else
        {
            // Default location preferences based on analysis
            locations.AddRange([
                "Remote in USA",
                "Atlanta, GA",
                "United States"
            ]);
        }

        return locations;
    }

    private List<string> GetExperienceLevels(ComprehensiveSearchRequest request)
    {
        // Based on 50 years experience profile
        return
        [
            "Senior",
            "Principal",
            "Lead",
            "Staff",
            "Expert or higher"
        ];
    }
}
