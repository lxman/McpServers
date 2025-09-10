using SeleniumChromeTool.Services.Scrapers;

namespace SeleniumChromeTool.Services;

public class JobSiteScraperFactory : IJobSiteScraperFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobSiteScraperFactory> _logger;

    public JobSiteScraperFactory(IServiceProvider serviceProvider, ILogger<JobSiteScraperFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IJobSiteScraper CreateScraper(JobSite site)
    {
        return site switch
        {
            JobSite.Dice => _serviceProvider.GetRequiredService<DiceScraper>(),
            JobSite.BuiltIn => _serviceProvider.GetRequiredService<BuiltInScraper>(),
            JobSite.AngelList => _serviceProvider.GetRequiredService<AngelListScraper>(),
            JobSite.StackOverflow => _serviceProvider.GetRequiredService<StackOverflowScraper>(),
            JobSite.HubSpot => _serviceProvider.GetRequiredService<HubSpotScraper>(),
            JobSite.SimplifyJobs => _serviceProvider.GetRequiredService<SimplifyJobsScraper>(),
            _ => throw new NotSupportedException($"Scraper for {site} is not implemented yet")
        };
    }

    public List<JobSite> GetSupportedSites()
    {
        return [JobSite.Indeed, JobSite.Dice, JobSite.LinkedIn, JobSite.Glassdoor, JobSite.BuiltIn, JobSite.AngelList, JobSite.RemoteOK, JobSite.StackOverflow, JobSite.HubSpot, JobSite.Zendesk, JobSite.Okta, JobSite.SimplifyJobs];
    }
}