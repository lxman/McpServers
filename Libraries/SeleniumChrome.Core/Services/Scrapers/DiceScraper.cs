using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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
            
            string searchUrl = BuildSearchUrl(request, config);
            Logger.LogInformation($"Scraping Dice: {searchUrl}");
            
            await Driver!.Navigate().GoToUrlAsync(searchUrl);
            await Task.Delay(1000);
            
            // Handle cookie consent dialog
            await HandleCookieConsent();
            
            // Smart wait for job content
            await WaitForJobContent();
            
            // Log page title for debugging
            Logger.LogInformation($"Page title: {Driver.Title}");
            
            // Store original window handle for tab management
            string originalWindow = Driver.CurrentWindowHandle;
            Logger.LogInformation($"Original window handle: {originalWindow}");
            
            // Try to find job cards with debugging
            ReadOnlyCollection<IWebElement> jobCards = Driver.FindElements(By.CssSelector(config.Selectors["jobCard"]));
            Logger.LogInformation($"Found {jobCards.Count} job cards on Dice using selector: {config.Selectors["jobCard"]}");
            
            // If no job cards found, try alternate selectors
            if (jobCards.Count == 0)
            {
                Logger.LogWarning("No job cards found with primary selector, trying alternatives...");
                string[] alternateSelectors =
                [
                    "[role='article'][data-testid='job-card']",
                    "[data-testid*='job']",
                    "[class*='job-card']",
                    "[class*='search-result']",
                    ".search-result-card",
                    ".job-listing"
                ];
                
                foreach (string altSelector in alternateSelectors)
                {
                    ReadOnlyCollection<IWebElement> altCards = Driver.FindElements(By.CssSelector(altSelector));
                    Logger.LogInformation($"Alternate selector '{altSelector}' found {altCards.Count} elements");
                    if (altCards.Count <= 0) continue;
                    jobCards = altCards;
                    break;
                }
            }

            var processedCount = 0;
            foreach (IWebElement card in jobCards.Take(request.MaxResults))
            {
                try
                {
                    processedCount++;
                    Logger.LogInformation($"Processing job {processedCount}/{Math.Min(jobCards.Count, request.MaxResults)}");
                    
                    EnhancedJobListing? job = await ExtractJobWithDetailPage(card, config, originalWindow);
                    if (job is not null)
                    {
                        job.SourceSite = SupportedSite;
                        jobs.Add(job);
                        Logger.LogInformation($"Successfully extracted job: {job.Title} at {job.Company}");
                    }
                    
                    await RespectRateLimit(config.RateLimit);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error extracting job from Dice card: {ex.Message}");
                }
            }
            
            Logger.LogInformation($"Completed scraping. Total jobs extracted: {jobs.Count}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error scraping Dice: {ex.Message}");
            throw;
        }
        
        return jobs;
    }

    private async Task<EnhancedJobListing?> ExtractJobWithDetailPage(IWebElement card, SiteConfiguration config, string originalWindow)
    {
        try
        {
            // Extract basic info from card first
            EnhancedJobListing? job = ExtractJobFromCard(card, config);
            if (job == null)
            {
                Logger.LogWarning("Failed to extract basic job info from card");
                return null;
            }

            // Now visit the detail page for additional information
            try
            {
                // Find the job detail link
                IWebElement titleElement = card.FindElement(By.CssSelector(config.Selectors["title"]));
                string jobUrl = titleElement.GetAttribute("href") ?? "";
                
                if (string.IsNullOrEmpty(jobUrl))
                {
                    Logger.LogWarning("No job URL found, skipping detail page extraction");
                    return job;
                }

                Logger.LogInformation($"Opening detail page: {jobUrl}");
                
                // Click the link to open detail page in new tab
                titleElement.Click();
                
                // Wait for new tab to open (timeout after 10 seconds)
                DateTime timeout = DateTime.Now.AddSeconds(10);
                while (Driver!.WindowHandles.Count <= 1 && DateTime.Now < timeout)
                {
                    await Task.Delay(100);
                }
                
                if (Driver.WindowHandles.Count <= 1)
                {
                    Logger.LogWarning("New tab did not open, skipping detail extraction");
                    return job;
                }
                
                // Switch to the new tab
                var detailWindow = "";
                foreach (string windowHandle in Driver.WindowHandles)
                {
                    if (windowHandle == originalWindow) continue;
                    detailWindow = windowHandle;
                    Driver.SwitchTo().Window(windowHandle);
                    Logger.LogInformation($"Switched to detail window: {windowHandle}");
                    break;
                }
                
                // Wait for detail page to load
                await Task.Delay(2000);
                
                // Extract detail page information
                await ExtractDetailPageInfo(job, config);
                
                // Close the detail tab
                Driver.Close();
                Logger.LogInformation("Closed detail tab");
                
                // Switch back to original window
                Driver.SwitchTo().Window(originalWindow);
                Logger.LogInformation("Switched back to search results");
                
                // Small delay to ensure we're back on the original page
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error during detail page extraction: {ex.Message}");
                
                // Ensure we're back on the original window
                try
                {
                    if (Driver!.WindowHandles.Contains(originalWindow))
                    {
                        Driver.SwitchTo().Window(originalWindow);
                    }
                }
                catch (Exception switchEx)
                {
                    Logger.LogError($"Error switching back to original window: {switchEx.Message}");
                }
            }

            return job;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error in ExtractJobWithDetailPage: {ex.Message}");
            return null;
        }
    }

    private async Task ExtractDetailPageInfo(EnhancedJobListing job, SiteConfiguration config)
    {
        try
        {
            // Wait for page to fully load
            DateTime timeout = DateTime.Now.AddSeconds(5);
            var pageLoaded = false;
            
            while (DateTime.Now < timeout && !pageLoaded)
            {
                try
                {
                    IWebElement h1 = Driver!.FindElement(By.TagName("h1"));
                    if (!string.IsNullOrEmpty(h1.Text))
                    {
                        pageLoaded = true;
                    }
                }
                catch
                {
                    await Task.Delay(200);
                }
            }

            // Extract Skills
            try
            {
                ReadOnlyCollection<IWebElement> skillChips = Driver!.FindElements(By.CssSelector(config.Selectors["skills"]));
                List<string> skills = skillChips.Select(chip => chip.Text.Trim()).Where(skillText => !string.IsNullOrEmpty(skillText) && skillText.Length < 50).ToList();

                if (skills.Any())
                {
                    job.RequiredSkills = skills;
                    Logger.LogInformation($"Extracted {skills.Count} skills: {string.Join(", ", skills.Take(5))}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error extracting skills: {ex.Message}");
            }

            // Extract Salary
            try
            {
                ReadOnlyCollection<IWebElement> salaryElements = Driver!.FindElements(By.TagName("span"));
                foreach (IWebElement elem in salaryElements)
                {
                    string text = elem.Text?.Trim() ?? "";
                    if (!text.Contains('$') ||
                        (!text.Contains("Up to") && !text.Contains('-') && !text.Contains("/hr"))) continue;
                    job.Salary = text;
                    Logger.LogInformation($"Extracted salary: {text}");
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error extracting salary: {ex.Message}");
            }

            // Extract Full Description
            try
            {
                // Try to find "Job Details" section
                ReadOnlyCollection<IWebElement> jobDetailsHeaders = Driver!.FindElements(By.XPath("//h2[contains(text(), 'Job Details')]"));
                if (jobDetailsHeaders.Any())
                {
                    IWebElement descriptionElement = jobDetailsHeaders[0].FindElement(By.XPath("following-sibling::div[1]"));
                    job.FullDescription = descriptionElement.Text?.Trim() ?? "";
                    Logger.LogInformation($"Extracted full description: {job.FullDescription.Length} characters");
                }
                else
                {
                    // Fallback: try to get text from body
                    string bodyText = Driver.FindElement(By.TagName("body")).Text;
                    if (!string.IsNullOrEmpty(bodyText) && bodyText.Length > 200)
                    {
                        job.Description = bodyText.Substring(0, Math.Min(500, bodyText.Length));
                        Logger.LogInformation("Extracted description from body text");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error extracting description: {ex.Message}");
            }

            // Extract Employment Type (Contract, Full-time, etc.)
            try
            {
                string pageText = Driver!.FindElement(By.TagName("body")).Text;
                
                if (pageText.Contains("Contract - W2"))
                    job.JobType = "Contract - W2";
                else if (pageText.Contains("Contract - C2C"))
                    job.JobType = "Contract - C2C";
                else if (pageText.Contains("Contract"))
                    job.JobType = "Contract";
                else if (pageText.Contains("Full-time") || pageText.Contains("Full Time"))
                    job.JobType = "Full-time";
                else if (pageText.Contains("Part-time") || pageText.Contains("Part Time"))
                    job.JobType = "Part-time";
                
                if (!string.IsNullOrEmpty(job.JobType))
                {
                    Logger.LogInformation($"Extracted job type: {job.JobType}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error extracting job type: {ex.Message}");
            }

            // Extract Experience Level
            try
            {
                string pageText = Driver!.FindElement(By.TagName("body")).Text;
                
                string[] experiencePatterns =
                [
                    @"(\d+-\d+\s+years)",
                    @"(\d+\+\s+years)",
                    @"Level Required.*?:\s*([^\n]+)",
                    @"Experience.*?:\s*([^\n]+)"
                ];

                foreach (string pattern in experiencePatterns)
                {
                    Match match = Regex.Match(pageText, pattern, RegexOptions.IgnoreCase);
                    if (!match.Success) continue;
                    job.ExperienceLevel = match.Groups[1].Value.Trim();
                    Logger.LogInformation($"Extracted experience level: {job.ExperienceLevel}");
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error extracting experience level: {ex.Message}");
            }

            // Extract Requirements
            try
            {
                string pageText = Driver!.FindElement(By.TagName("body")).Text;
                var requirements = new List<string>();
                
                // Look for "Additional Skills" or "Requirements" sections
                string[] lines = pageText.Split('\n');
                var inRequirementsSection = false;
                
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    
                    if (trimmedLine.Contains("Additional Skills") || 
                        trimmedLine.Contains("Requirements") ||
                        trimmedLine.Contains("Qualifications"))
                    {
                        inRequirementsSection = true;
                        continue;
                    }

                    if (!inRequirementsSection) continue;
                    // Stop if we hit another section
                    if (trimmedLine.StartsWith("Level Required") || 
                        trimmedLine.StartsWith("Employers have access") ||
                        trimmedLine.StartsWith("Dice Id"))
                    {
                        break;
                    }
                        
                    // Add requirement lines that look meaningful
                    if (trimmedLine.Length > 10 && trimmedLine.Length < 200 &&
                        (trimmedLine.Contains("year") || trimmedLine.Contains("experience") || 
                         trimmedLine.Contains("knowledge") || trimmedLine.StartsWith("-") ||
                         trimmedLine.StartsWith("•")))
                    {
                        requirements.Add(trimmedLine.TrimStart('-', '•', ' '));
                    }
                }
                
                if (requirements.Any())
                {
                    job.Requirements = requirements;
                    Logger.LogInformation($"Extracted {requirements.Count} requirements");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error extracting requirements: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ExtractDetailPageInfo: {ex.Message}");
        }
    }

    private async Task WaitForJobContent()
    {
        try
        {
            // Smart wait: Wait for job cards to appear OR timeout after 3 seconds
            DateTime timeout = DateTime.Now.AddSeconds(3);
            var contentFound = false;
            
            while (DateTime.Now < timeout && !contentFound)
            {
                try
                {
                    // Check if Dice job cards are available using the correct selector
                    ReadOnlyCollection<IWebElement>? elements = Driver?.FindElements(By.CssSelector("[role='article'][data-testid='job-card'], [class*='job-card']"));
                    if (elements is { Count: > 0 })
                    {
                        contentFound = true;
                        Logger.LogInformation("Dice job content detected after smart wait");
                        break;
                    }
                    
                    // Also check for Dice-specific content patterns
                    string? pageSource = Driver?.PageSource;
                    if (pageSource is not null && (pageSource.Contains("job-card") || pageSource.Contains("Search Results")))
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
            await Task.Delay(500);
            
            // Try to find and click "Accept all" button
            string[] acceptButtons =
            [
                "Accept all",
                "Accept All",
                "button[data-testid='cookie-accept-all']",
                "button[id*='accept']",
                "[data-cy='accept-all-button']"
            ];

            foreach (string buttonSelector in acceptButtons)
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

                    if (acceptButton is not { Displayed: true, Enabled: true }) continue;
                    acceptButton.Click();
                    Logger.LogInformation("Clicked cookie consent button");
                    await Task.Delay(300);
                    return;
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
            IWebElement titleElement = card.FindElement(By.CssSelector(config.Selectors["title"]));
            string title = titleElement.Text.Trim();
            string jobUrl = titleElement.GetAttribute("href") ?? "";
            
            // Extract company - it's in a specific structure
            var company = "";
            try 
            {
                IWebElement companyElement = card.FindElement(By.CssSelector(config.Selectors["company"]));
                company = companyElement.Text?.Trim() ?? "";
            }
            catch (NoSuchElementException) 
            {
                // Fallback to find any company link
                try
                {
                    IWebElement companyLink = card.FindElement(By.CssSelector("a[href*='/company-profile']"));
                    company = companyLink.Text.Trim();
                }
                catch (NoSuchElementException) { }
            }

            // Extract location - it's in the .text-zinc-600 elements
            var location = "";
            try
            {
                ReadOnlyCollection<IWebElement> locationElements = card.FindElements(By.CssSelector(".text-zinc-600"));
                foreach (IWebElement elem in locationElements)
                {
                    string text = elem.Text?.Trim() ?? "";
                    if (string.IsNullOrEmpty(text) || text == "•" || text == "Today" ||
                        text.Contains("Posted") || text.Contains("ago")) continue;
                    location = text;
                    break;
                }
            }
            catch (NoSuchElementException) { }

            // Extract summary
            var summary = "";
            try
            {
                IWebElement summaryElement = card.FindElement(By.CssSelector(config.Selectors["summary"]));
                summary = summaryElement.Text.Trim();
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
            return parent.FindElement(By.CssSelector(selector)).Text.Trim();
        }
        catch (NoSuchElementException)
        {
            return "";
        }
    }

    private static bool IsRemoteJob(string location, string title, string summary)
    {
        string text = $"{location} {title} {summary}".ToLower();
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
                ["jobCard"] = "[role='article'][data-testid='job-card']",
                ["title"] = "a[href*='/job-detail/']",
                ["company"] = "a[href*='/company-profile/']",
                ["location"] = ".text-zinc-600",
                ["summary"] = ".line-clamp-2",
                ["jobDetailTitle"] = "h1",
                ["skills"] = "[class*='chip']",
                ["salary"] = "span",
                ["description"] = "h2",
                ["searchInput"] = "#typeaheadInput",
                ["locationInput"] = "#google-location-search",
                ["submitButton"] = "#submitSearch-button"
            },
            UrlParameters = new Dictionary<string, string>
            {
                ["q"] = "q",
                ["location"] = "location"
            },
            RateLimit = new RateLimitConfig
            {
                RequestsPerMinute = 15,
                DelayBetweenRequests = 2000,
                RetryAttempts = 3,
                RetryDelay = 3000
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