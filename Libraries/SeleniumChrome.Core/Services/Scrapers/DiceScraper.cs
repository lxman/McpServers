using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services.Scrapers;

public class DiceScraper(ILogger<DiceScraper> logger) : BaseJobScraper(logger)
{
    public override JobSite SupportedSite => JobSite.Dice;

    public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            InitializeDriver(config.AntiDetection);
            
            var searchUrl = BuildSearchUrl(request, config);
            Logger.LogInformation($"Scraping Dice: {searchUrl}");
            
            Driver!.Navigate().GoToUrl(searchUrl);
            await Task.Delay(1000); // Reduced from 3000ms
            
            // Handle cookie consent dialog
            await HandleCookieConsent();
            
            // Smart wait for job content
            await WaitForJobContent();
            
            // Log page title for debugging
            Logger.LogInformation($"Page title: {Driver.Title}");
            
            // Try to find job cards with debugging
            var jobCards = Driver.FindElements(By.CssSelector(config.Selectors["jobCard"]));
            Logger.LogInformation($"Found {jobCards.Count} job cards on Dice using selector: {config.Selectors["jobCard"]}");
            
            // If no job cards found, try alternate selectors
            if (jobCards.Count == 0)
            {
                Logger.LogWarning("No job cards found with primary selector, trying alternatives...");
                var alternateSelectors = new[]
                {
                    "[data-testid*='job']",
                    "[class*='job-card']",
                    "[class*='search-result']",
                    ".search-result-card",
                    ".job-listing"
                };
                
                foreach (var altSelector in alternateSelectors)
                {
                    var altCards = Driver.FindElements(By.CssSelector(altSelector));
                    Logger.LogInformation($"Alternate selector '{altSelector}' found {altCards.Count} elements");
                    if (altCards.Count > 0)
                    {
                        jobCards = altCards;
                        break;
                    }
                }
            }

            foreach (var card in jobCards.Take(request.MaxResults))
            {
                try
                {
                    var job = ExtractJobFromCard(card, config);
                    if (job != null)
                    {
                        job.SourceSite = SupportedSite;
                        jobs.Add(job);
                    }
                    
                    await RespectRateLimit(config.RateLimit);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error extracting job from Dice card: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error scraping Dice: {ex.Message}");
            throw;
        }
        
        return jobs;
    }

    private async Task WaitForJobContent()
    {
        try
        {
            // Smart wait: Wait for job cards to appear OR timeout after 2 seconds
            var timeout = DateTime.Now.AddSeconds(2);
            var contentFound = false;
            
            while (DateTime.Now < timeout && !contentFound)
            {
                try
                {
                    // Check if Dice job cards are available
                    var elements = Driver.FindElements(By.CssSelector("[data-testid='job-search-serp-card'], [class*='job-card'], [class*='search-result']"));
                    if (elements.Count > 0)
                    {
                        contentFound = true;
                        Logger.LogInformation("Dice job content detected after smart wait");
                        break;
                    }
                    
                    // Also check for Dice-specific content patterns
                    var pageSource = Driver.PageSource;
                    if (pageSource.Contains("job-search-serp-card") || pageSource.Contains("Search Results"))
                    {
                        contentFound = true;
                        Logger.LogInformation("Dice search results detected in page source");
                        break;
                    }
                    
                    await Task.Delay(100); // Small delay before checking again
                }
                catch
                {
                    // Continue waiting if elements not found yet
                    await Task.Delay(100);
                }
            }
            
            if (!contentFound)
            {
                Logger.LogInformation("Smart wait timeout - proceeding with available content");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error in smart wait: {ex.Message}");
            // Fall back to small delay if smart wait fails
            await Task.Delay(500);
        }
    }

    private async Task HandleCookieConsent()
    {
        try
        {
            // Reduced wait for cookie dialog
            await Task.Delay(500); // Reduced from 2000ms
            
            // Try to find and click "Accept all" button
            var acceptButtons = new[]
            {
                "Accept all",
                "Accept All",
                "button[data-testid='cookie-accept-all']",
                "button[id*='accept']",
                "[data-cy='accept-all-button']"
            };

            foreach (var buttonSelector in acceptButtons)
            {
                try
                {
                    IWebElement? acceptButton;
                    if (buttonSelector.Contains("[") || buttonSelector.Contains("button"))
                    {
                        acceptButton = Driver!.FindElement(By.CssSelector(buttonSelector));
                    }
                    else
                    {
                        acceptButton = Driver!.FindElement(By.XPath($"//button[contains(text(), '{buttonSelector}')]"));
                    }
                    
                    if (acceptButton is { Displayed: true, Enabled: true })
                    {
                        acceptButton.Click();
                        Logger.LogInformation("Clicked cookie consent button");
                        await Task.Delay(300); // Reduced from 1000ms
                        return;
                    }
                }
                catch (NoSuchElementException)
                {
                }
            }
            
            Logger.LogInformation("No cookie consent dialog found or already handled");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling cookie consent: {ex.Message}");
        }
    }

    private static string BuildSearchUrl(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        var baseUrl = $"{config.BaseUrl}{config.SearchEndpoint}";
        var queryParams = new List<string>
        {
            $"{config.UrlParameters["q"]}={Uri.EscapeDataString(request.SearchTerm)}",
            $"{config.UrlParameters["location"]}={Uri.EscapeDataString(request.Location)}"
        };
        
        return $"{baseUrl}?{string.Join("&", queryParams)}";
    }

    private EnhancedJobListing? ExtractJobFromCard(IWebElement card, SiteConfiguration config)
    {
        try
        {
            // Extract title and URL
            var titleElement = card.FindElement(By.CssSelector(config.Selectors["title"]));
            var title = titleElement.Text?.Trim() ?? "";
            var jobUrl = titleElement.GetAttribute("href") ?? "";
            
            // Extract company - it's in a specific structure
            var company = "";
            try 
            {
                var companyElement = card.FindElement(By.CssSelector(config.Selectors["company"]));
                company = companyElement.Text?.Trim() ?? "";
            }
            catch (NoSuchElementException) 
            {
                // Fallback to find any company link
                try
                {
                    var companyLink = card.FindElement(By.CssSelector("a[href*='/company-profile']"));
                    company = companyLink.Text?.Trim() ?? "";
                }
                catch (NoSuchElementException) { }
            }

            // Extract location - it's in the .text-zinc-600 elements
            var location = "";
            try
            {
                var locationElements = card.FindElements(By.CssSelector(".text-zinc-600"));
                foreach (var elem in locationElements)
                {
                    var text = elem.Text?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(text) && text != "•" && text != "Today" && 
                        !text.Contains("Posted") && !text.Contains("ago"))
                    {
                        location = text;
                        break;
                    }
                }
            }
            catch (NoSuchElementException) { }

            // Extract summary
            var summary = "";
            try
            {
                var summaryElement = card.FindElement(By.CssSelector(config.Selectors["summary"]));
                summary = summaryElement.Text?.Trim() ?? "";
            }
            catch (NoSuchElementException) { }
            
            var job = new EnhancedJobListing
            {
                Title = title,
                Company = company,
                Location = location,
                Summary = summary,
                Url = jobUrl,
                DatePosted = DateTime.Now
            };

            job.IsRemote = IsRemoteJob(job.Location, job.Title, job.Summary);
            return job;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting Dice job details: {ex.Message}");
            return null;
        }
    }

    private static string ExtractText(IWebElement parent, string selector)
    {
        try
        {
            return parent.FindElement(By.CssSelector(selector))?.Text?.Trim() ?? "";
        }
        catch (NoSuchElementException)
        {
            return "";
        }
    }

    private static bool IsRemoteJob(string location, string title, string summary)
    {
        var text = $"{location} {title} {summary}".ToLower();
        return text.Contains("remote") || text.Contains("work from home") || text.Contains("telecommute");
    }

    public override SiteConfiguration GetDefaultConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "Dice",
            BaseUrl = "https://www.dice.com",
            SearchEndpoint = "/jobs",
            Selectors = new Dictionary<string, string>
            {
                ["jobCard"] = "[data-testid=\"job-search-serp-card\"]",
                ["title"] = "[data-testid=\"job-search-job-detail-link\"]",
                ["company"] = "a[href*=\"/company-profile\"] p",
                ["location"] = ".text-zinc-600",
                ["summary"] = ".line-clamp-2"
            },
            UrlParameters = new Dictionary<string, string>
            {
                ["q"] = "q",
                ["location"] = "location"
            },
            RateLimit = new RateLimitConfig
            {
                RequestsPerMinute = 20,          // Increased from 15
                DelayBetweenRequests = 1000,     // Reduced from 2000ms
                RetryAttempts = 3,
                RetryDelay = 3000                // Reduced from 5000ms
            },
            AntiDetection = new AntiDetectionConfig
            {
                UserAgents =
                [
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36"
                ],
                RequiresCookieAccept = false,
                UsesCloudflare = false
            },
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
    }
}
