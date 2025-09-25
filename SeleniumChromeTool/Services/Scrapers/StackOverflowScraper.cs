using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services.Scrapers;

public partial class StackOverflowScraper : BaseJobScraper
{
    public override JobSite SupportedSite => JobSite.StackOverflow;

    public StackOverflowScraper(ILogger<StackOverflowScraper> logger) : base(logger)
    {
    }

    public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        var jobs = new List<EnhancedJobListing>();

        try
        {
            Logger.LogInformation("Starting Stack Overflow careers scraping for user: {UserId}", request.UserId);

            InitializeDriver(config.AntiDetection);
            await NavigateToJobsPage(Driver!, config);

            // Wait for page to load
            await Task.Delay(3000);

            // Handle any modals or popups
            await DismissModalsAsync(Driver!);
            
            // Extract job listings
            jobs = await ExtractJobListings(Driver!, request);

            Logger.LogInformation("Successfully scraped {JobCount} jobs from Stack Overflow", jobs.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error scraping Stack Overflow careers page");
        }
        finally
        {
            Dispose();
        }

        return jobs;
    }

    private async Task NavigateToJobsPage(IWebDriver driver, SiteConfiguration config)
    {
        var careersUrl = "https://stackoverflow.co/company/work-here/";
        
        Logger.LogInformation("Navigating to Stack Overflow careers: {Url}", careersUrl);
        driver.Navigate().GoToUrl(careersUrl);

        // Wait for the page to load
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));        
        try
        {
            // Wait for the main content to load
            wait.Until(d => d.FindElements(By.CssSelector("main, .main-content, body")).Count > 0);
        }
        catch (WebDriverTimeoutException)
        {
            Logger.LogWarning("Timeout waiting for Stack Overflow careers page to load");
        }
    }

    public override async Task<bool> TestSiteAccessibilityAsync(SiteConfiguration config)
    {
        try
        {
            InitializeDriver(config.AntiDetection);
            Driver!.Navigate().GoToUrl("https://stackoverflow.co/company/work-here/");
            await Task.Delay(5000);
            var hasContent = Driver.FindElements(By.CssSelector("main, .main-content, body")).Count > 0;
            return hasContent;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error testing Stack Overflow accessibility");
            return false;
        }
        finally
        {
            Dispose();
        }
    }

    public override SiteConfiguration GetDefaultConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "StackOverflow",
            BaseUrl = "https://stackoverflow.co",
            JobsUrl = "https://stackoverflow.co/company/work-here/",
            SupportedSearchTerms = [".NET", "C#", "Backend", "Full Stack", "Software Engineer"],
            CssSelectors = new Dictionary<string, string>
            {
                ["JobContainer"] = ".job-listing, .careers-position, [data-testid='job-card']",
                ["JobTitle"] = ".job-title, h3 a, [data-testid='job-title']",
                ["JobLocation"] = ".job-location, .location, [data-testid='location']",
                ["JobDescription"] = ".job-description, .description, .summary",
                ["ApplyLink"] = ".apply-link, .job-link a, a[href*='job']"
            },
            RateLimitConfig = new RateLimitConfig
            {
                RequestsPerMinute = 10,
                DelayBetweenRequests = 4000
            },
            IsActive = true,
            RequiresJavaScript = true
        };
    }
}
