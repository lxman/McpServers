using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services;

public interface IJobSiteScraper
{
    JobSite SupportedSite { get; }
    Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config);
    Task<bool> TestSiteAccessibilityAsync(SiteConfiguration config);
    SiteConfiguration GetDefaultConfiguration();
}