namespace SeleniumChromeTool.Services;

public interface IJobSiteScraperFactory
{
    IJobSiteScraper CreateScraper(JobSite site);
    List<JobSite> GetSupportedSites();
}