using System.Collections.ObjectModel;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services.Scrapers;

public partial class HubSpotScraper : BaseJobScraper
{
    public override JobSite SupportedSite => JobSite.HubSpot;

    public HubSpotScraper(ILogger<HubSpotScraper> logger) : base(logger)
    {
    }

    public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        var jobs = new List<EnhancedJobListing>();

        try
        {
            Logger.LogInformation("Starting HubSpot careers scraping for user: {UserId}", request.UserId);

            InitializeDriver(config.AntiDetection);
            await NavigateToJobsPage(Driver!, config, request);

            // Wait for page to load (HubSpot uses React, needs more time)
            await Task.Delay(5000);

            // Handle any modals or popups
            await DismissModalsAsync(Driver!);

            // Wait for job listings to load
            await WaitForJobsToLoad(Driver!);

            // Extract job listings
            jobs = await ExtractJobListings(Driver!, request);

            Logger.LogInformation("Successfully scraped {JobCount} jobs from HubSpot", jobs.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error scraping HubSpot careers page");
        }
        finally
        {
            Dispose();
        }

        return jobs;
    }
    private async Task NavigateToJobsPage(IWebDriver driver, SiteConfiguration config, EnhancedScrapeRequest request)
    {
        var careersUrl = "https://www.hubspot.com/careers/jobs";
        
        // Add search parameters if we have a search term
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            // HubSpot careers page uses query parameters for search
            string searchTerm = Uri.EscapeDataString(request.SearchTerm);
            careersUrl += $"?search={searchTerm}";
        }
        
        Logger.LogInformation("Navigating to HubSpot careers: {Url}", careersUrl);
        driver.Navigate().GoToUrl(careersUrl);

        // Wait for the page to load - HubSpot uses React heavily
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
        
        try
        {
            // Wait for the main content or job listings container to appear
            wait.Until(d => d.FindElements(By.CssSelector("main, .careers-page, .job-listings, [data-testid='job-search-results']")).Count > 0);
        }
        catch (WebDriverTimeoutException)
        {
            Logger.LogWarning("Timeout waiting for HubSpot careers page to load");
        }
    }

    private async Task DismissModalsAsync(IWebDriver driver)
    {
        try
        {
            // HubSpot common modal/popup selectors
            var modalSelectors = new[]
            {
                // Cookie consent
                "[data-testid='cookie-banner'] button",
                ".cookie-consent button",
                ".onetrust-close-btn-ui",
                "#onetrust-accept-btn-handler",
                
                // General modals
                ".modal-close",
                ".popup-close",
                "[aria-label='Close']",
                ".close-button",
                
                // HubSpot specific
                ".hs-popup-close",
                ".popup-close-simple",
                "[data-module-id*='popup'] .close"
            };

            foreach (string selector in modalSelectors)
            {
                try
                {
                    ReadOnlyCollection<IWebElement>? closeButtons = driver.FindElements(By.CssSelector(selector));
                    foreach (var closeButton in closeButtons)
                    {
                        if (closeButton?.Displayed == true && closeButton.Enabled)
                        {
                            closeButton.Click();
                            await Task.Delay(1000);
                            Logger.LogInformation("Dismissed modal with selector: {Selector}", selector);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug("Could not dismiss modal with selector {Selector}: {Error}", selector, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error dismissing modals");
        }
    }

    private async Task WaitForJobsToLoad(IWebDriver driver)
    {
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
            
            // Wait for either job listings to appear or a "no results" message
            wait.Until(d => 
                d.FindElements(By.CssSelector(
                    ".job-posting, .career-opportunity, .job-listing, [data-testid='job-card'], .no-results, .empty-state"
                )).Count > 0
            );
            
            // Additional wait for React rendering
            await Task.Delay(2000);
        }
        catch (WebDriverTimeoutException)
        {
            Logger.LogWarning("Timeout waiting for HubSpot job listings to load");
        }
    }
    private async Task<List<EnhancedJobListing>> ExtractJobListings(IWebDriver driver, EnhancedScrapeRequest request)
    {
        var jobs = new List<EnhancedJobListing>();

        try
        {
            // HubSpot job container selectors (they change frequently due to React)
            var jobContainerSelectors = new[]
            {
                ".job-posting",
                ".career-opportunity",
                ".job-listing",
                "[data-testid='job-card']",
                ".careers-job-item",
                ".job-item",
                ".position-card",
                "article[data-job-id]",
                ".job-search-result",
                "div[role='article']"
            };

            IList<IWebElement> jobElements = new List<IWebElement>();

            // Try different selectors to find job listings
            foreach (string selector in jobContainerSelectors)
            {
                jobElements = driver.FindElements(By.CssSelector(selector));
                if (jobElements.Count <= 0) continue;
                Logger.LogInformation("Found {Count} job elements using selector: {Selector}", jobElements.Count, selector);
                break;
            }

            if (jobElements.Count == 0)
            {
                Logger.LogWarning("No job listings found on HubSpot careers page");
                
                // Check if there's a "no results" message
                var noResultsSelectors = new[] { ".no-results", ".empty-state", ".no-jobs", ".zero-results" };
                if (noResultsSelectors.Select(selector => driver.FindElements(By.CssSelector(selector)).FirstOrDefault()).Any(noResultsElement => noResultsElement?.Displayed == true))
                {
                    Logger.LogInformation("Found 'no results' message on HubSpot careers page");
                }
                
                return jobs;
            }

            // Process each job listing
            var processedJobs = 0;
            foreach (IWebElement jobElement in jobElements)
            {
                if (processedJobs >= request.MaxResults)
                    break;

                try
                {
                    EnhancedJobListing? job = ExtractJobDetails(jobElement, driver).Result;
                    
                    if (job != null && IsRelevantJob(job, request))
                    {
                        jobs.Add(job);
                        processedJobs++;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error extracting individual job details");
                }

                // Add delay between processing jobs
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error extracting job listings from HubSpot");
        }

        return jobs;
    }
    public override async Task<bool> TestSiteAccessibilityAsync(SiteConfiguration config)
    {
        try
        {
            InitializeDriver(config.AntiDetection);
            Driver!.Navigate().GoToUrl("https://www.hubspot.com/careers/jobs");
            
            // Wait for the page to load
            await Task.Delay(8000);
            
            // Check if we can find the main content
            bool hasContent = Driver.FindElements(By.CssSelector("main, .careers-page, .job-listings, body")).Count > 0;
            
            // Also check if we're not blocked (look for error pages)
            ReadOnlyCollection<IWebElement>? errorIndicators = Driver.FindElements(By.CssSelector(".error, .blocked, .forbidden, .not-found"));
            bool isBlocked = errorIndicators.Any(e => e.Displayed);
            
            return hasContent && !isBlocked;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error testing HubSpot accessibility");
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
            SiteName = "HubSpot",
            BaseUrl = "https://www.hubspot.com",
            JobsUrl = "https://www.hubspot.com/careers/jobs",
            SupportedSearchTerms = [".NET", "C#", "Backend", "Full Stack", "Software Engineer", "API", "Microservices"],
            CssSelectors = new Dictionary<string, string>
            {
                ["JobContainer"] = ".job-posting, .career-opportunity, [data-testid='job-card']",
                ["JobTitle"] = ".job-title, h3 a, [data-testid='job-title'], .position-title",
                ["JobLocation"] = ".job-location, .location, [data-testid='location'], .office-location",
                ["JobDepartment"] = ".department, .team, [data-testid='department'], .job-category",
                ["JobDescription"] = ".job-description, .description, .summary, .job-summary",
                ["ApplyLink"] = ".apply-button, .apply-link, a[href*='apply'], .job-link"
            },
            RateLimitConfig = new RateLimitConfig
            {
                RequestsPerMinute = 15,
                DelayBetweenRequests = 3000
            },
            IsActive = true,
            RequiresJavaScript = true
        };
    }
}