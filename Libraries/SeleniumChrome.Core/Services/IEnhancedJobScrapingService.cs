using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services;

public interface IEnhancedJobScrapingService
{
    Task<List<EnhancedJobListing>> ScrapeMultipleSitesAsync(EnhancedScrapeRequest request, string? sessionId = null);
    Task<List<EnhancedJobListing>> ScrapeSpecificSiteAsync(JobSite site, EnhancedScrapeRequest request, string? sessionId = null);
    Task<SiteConfiguration> GetSiteConfigurationAsync(JobSite site);
    Task UpdateSiteConfigurationAsync(SiteConfiguration config);
    Task<List<EnhancedJobListing>> GetStoredJobsAsync(string userId, JobSearchFilters filters);
    Task<string> TakeScreenshotAsync(string url);
    Task<bool> SaveJobsAsync(SaveJobsRequest request);

    // Temporary collection methods for resilience
    Task SaveToTemporaryCollectionAsync(List<EnhancedJobListing> jobs, string sessionId, int batchNumber, string operationType, string searchTerm, string location);
    Task<ConsolidationResult> ConsolidateTemporaryResultsAsync(string sessionId, string userId);
    Task<List<TemporaryJobListing>> GetTemporaryResultsAsync(string? sessionId = null, bool includeConsolidated = false);
    Task<int> CleanupOldTemporaryResultsAsync();
}