using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services;

public interface IEnhancedJobScrapingService
{
    Task<List<EnhancedJobListing>> ScrapeMultipleSitesAsync(EnhancedScrapeRequest request);
    Task<List<EnhancedJobListing>> ScrapeSpecificSiteAsync(JobSite site, EnhancedScrapeRequest request);
    Task<SiteConfiguration> GetSiteConfigurationAsync(JobSite site);
    Task UpdateSiteConfigurationAsync(SiteConfiguration config);
    Task<List<EnhancedJobListing>> GetStoredJobsAsync(string userId, JobSearchFilters filters);
    Task<string> TakeScreenshotAsync(string url);
    Task<bool> SaveJobsAsync(SaveJobsRequest request);
}