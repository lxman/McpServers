using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeleniumChrome.Core.Services.Scrapers;

namespace SeleniumChrome.Core.Services;

public class JobSiteScraperFactory(IServiceProvider serviceProvider, ILogger<JobSiteScraperFactory> logger)
    : IJobSiteScraperFactory
{
    private readonly ILogger<JobSiteScraperFactory> _logger = logger;

    public IJobSiteScraper CreateScraper(JobSite site)
    {
        return site switch
        {
            JobSite.Dice => serviceProvider.GetRequiredService<DiceScraper>(),
            JobSite.BuiltIn => serviceProvider.GetRequiredService<BuiltInScraper>(),
            JobSite.AngelList => serviceProvider.GetRequiredService<AngelListScraper>(),
            JobSite.StackOverflow => serviceProvider.GetRequiredService<StackOverflowScraper>(),
            JobSite.HubSpot => serviceProvider.GetRequiredService<HubSpotScraper>(),
            JobSite.SimplifyJobs => serviceProvider.GetRequiredService<SimplifyJobsScraper>(),
            _ => throw new NotSupportedException($"Scraper for {site} is not implemented yet")
        };
    }

    public List<JobSite> GetSupportedSites()
    {
        return [JobSite.Indeed, JobSite.Dice, JobSite.LinkedIn, JobSite.Glassdoor, JobSite.BuiltIn, JobSite.AngelList, JobSite.RemoteOK, JobSite.StackOverflow, JobSite.HubSpot, JobSite.Zendesk, JobSite.Okta, JobSite.SimplifyJobs];
    }
}