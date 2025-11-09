using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services.Scrapers;

public class AngelListScraper(ILogger<AngelListScraper> logger) : BaseJobScraper(logger)
{
    public override JobSite SupportedSite => JobSite.AngelList;

    public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            InitializeDriver(config.AntiDetection);
            
            string searchUrl = BuildSearchUrl(request, config);
            Logger.LogInformation($"Scraping AngelList: {searchUrl}");
            
            // Navigate to page with reduced delay
            await Driver!.Navigate().GoToUrlAsync(searchUrl);
            await Task.Delay(1000);
            
            Logger.LogInformation($"Page title: {Driver.Title}");
            Logger.LogInformation($"Page URL: {Driver.Url}");
            
            // Handle any modals or popups
            await HandlePopups();
            
            // Smart wait for job content
            await WaitForJobContent();
            
            // Find job elements using multiple strategies
            var jobElements = new ReadOnlyCollection<IWebElement>(new List<IWebElement>());
            
            string[] jobSelectors =
            [
                ".job-listings .job-listing",          // Main job listing container
                "[data-test='job-result']",            // Job result cards
                ".styles_component__UCLp8",            // Wellfound specific class patterns
                ".startup-link",                       // Startup job links
                "a[href*='/company/'][href*='/jobs/']", // Company job URLs
                "[data-cy='job-card']",                // Job card elements
                ".job-card-wrapper",                   // Job card wrappers
                "a[href*='/jobs/'][title]"             // Job links with titles
            ];
            
            // Try selectors in order of likelihood
            foreach (string selector in jobSelectors)
            {
                try
                {
                    jobElements = Driver.FindElements(By.CssSelector(selector));
                    Logger.LogInformation($"Selector '{selector}' found {jobElements.Count} elements");
                    if (jobElements.Count > 0) 
                    {
                        Logger.LogInformation($"Using selector: {selector}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Selector '{selector}' failed: {ex.Message}");
                }
            }
            
            // Process found job elements
            if (jobElements.Count > 0)
            {
                Logger.LogInformation($"Processing {jobElements.Count} AngelList job elements");
                
                foreach (IWebElement element in jobElements.Take(request.MaxResults))
                {
                    try
                    {
                        EnhancedJobListing? job = ExtractJobFromElement(element);
                        if (job is not null)
                        {
                            job.SourceSite = SupportedSite;
                            // Add startup-specific tags
                            job.RequiredSkills = job.RequiredSkills ?? [];
                            if (!job.RequiredSkills.Contains("Startup"))
                            {
                                job.RequiredSkills.Add("Startup");
                            }
                            
                            jobs.Add(job);
                            Logger.LogInformation($"Extracted AngelList job: {job.Title} at {job.Company} - URL: {job.Url}");
                        }
                        
                        await RespectRateLimit(config.RateLimit);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error extracting job from AngelList element: {ex.Message}");
                    }
                }
            }
            else
            {
                // Fallback: Try to extract from page content
                Logger.LogInformation("No job elements found, attempting content extraction");
                await ExtractFromPageContent(jobs, request.MaxResults);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error scraping AngelList: {ex.Message}");
            Logger.LogError($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            // Ensure driver is properly disposed
            try
            {
                Driver?.Quit();
                Driver?.Dispose();
                Driver = null;
            }
            catch (Exception disposeEx)
            {
                Logger.LogWarning($"Error disposing driver: {disposeEx.Message}");
            }
        }
        
        Logger.LogInformation($"AngelList scraping completed. Found {jobs.Count} jobs");
        return jobs;
    }

    private async Task WaitForJobContent()
    {
        try
        {
            // Smart wait for AngelList content to load
            DateTime timeout = DateTime.Now.AddSeconds(3); // Slightly longer for AngelList
            var contentFound = false;
            
            while (DateTime.Now < timeout && !contentFound)
            {
                try
                {
                    // Check for AngelList-specific job content
                    ReadOnlyCollection<IWebElement> elements = Driver.FindElements(By.CssSelector("[data-test='startup-job'], .job-card, a[href*='/j/'], a[href*='/jobs/'][href*='-at-']"));
                    if (elements.Count > 0)
                    {
                        contentFound = true;
                        Logger.LogInformation("AngelList job content detected after smart wait");
                        break;
                    }
                    
                    // Check for startup-related content in page source
                    string pageSource = Driver.PageSource;
                    if (pageSource.Contains("-at-") || pageSource.Contains("startup-job") || pageSource.Contains("job-posting"))
                    {
                        contentFound = true;
                        Logger.LogInformation("AngelList job patterns detected in page source");
                        break;
                    }
                    
                    await Task.Delay(100);
                }
                catch
                {
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
            await Task.Delay(500);
        }
    }

    private async Task HandlePopups()
    {
        try
        {
            await Task.Delay(500);
            
            // Common popup dismissal patterns for AngelList
            string[] dismissSelectors =
            [
                "[data-test='dismiss']",
                "[aria-label='Close']",
                ".modal-close",
                ".popup-close",
                "button[class*='close']",
                "[data-dismiss='modal']"
            ];

            foreach (string selector in dismissSelectors)
            {
                try
                {
                    IWebElement closeButton = Driver.FindElement(By.CssSelector(selector));
                    if (closeButton is { Displayed: true, Enabled: true })
                    {
                        closeButton.Click();
                        Logger.LogInformation("Dismissed AngelList popup");
                        await Task.Delay(300);
                        return;
                    }
                }
                catch (NoSuchElementException)
                {
                }
            }
            
            Logger.LogInformation("No AngelList popups found to dismiss");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling popups: {ex.Message}");
        }
    }

    private static string BuildSearchUrl(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        // Try a more direct approach to actual job listings
        string baseUrl = config.BaseUrl; // https://wellfound.com
        
        // Build more specific search for actual job postings
        var searchPath = "/role/r/software-engineer";
        
        var queryParams = new List<string>();
        
        // Add remote filter for remote jobs
        if (!string.IsNullOrEmpty(request.Location) && 
            request.Location.Contains("Remote", StringComparison.OrdinalIgnoreCase))
        {
            queryParams.Add("remote=true");
        }
        
        // Add technology filter for .NET
        if (request.SearchTerm.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
            request.SearchTerm.Contains("C#", StringComparison.OrdinalIgnoreCase))
        {
            queryParams.Add("tech=dotnet");
        }
        
        if (queryParams.Count > 0)
        {
            searchPath += "?" + string.Join("&", queryParams);
        }
        
        return $"{baseUrl}{searchPath}";
    }

    private EnhancedJobListing? ExtractJobFromElement(IWebElement element)
    {
        try
        {
            string elementText = element.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(elementText)) return null;
            
            var title = "";
            var company = "";
            var location = "";
            var summary = "";
            var jobUrl = "";
            
            // Try to extract title from various AngelList structures
            try
            {
                // Common AngelList title patterns
                string[] titleSelectors =
                [
                    "[data-test='job-title']",
                    "h3",
                    "h2", 
                    ".job-title",
                    "a[href*='/jobs/']"
                ];
                
                foreach (string selector in titleSelectors)
                {
                    try
                    {
                        IWebElement titleElement = element.FindElement(By.CssSelector(selector));
                        title = titleElement.Text?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(title) && title.Length > 3)
                        {
                            break;
                        }
                    }
                    catch (NoSuchElementException) { }
                }
            }
            catch (Exception) { }
            
            // Try to extract company name
            try
            {
                string[] companySelectors =
                [
                    "[data-test='company-name']",
                    ".company-name",
                    "[class*='company']",
                    "a[href*='/company/']"
                ];
                
                foreach (string selector in companySelectors)
                {
                    try
                    {
                        IWebElement companyElement = element.FindElement(By.CssSelector(selector));
                        company = companyElement.Text?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(company) && company.Length > 1)
                        {
                            break;
                        }
                    }
                    catch (NoSuchElementException) { }
                }
            }
            catch (Exception) { }
            
            // Try to extract URL
            try
            {
                IWebElement linkElement = element.FindElement(By.TagName("a"));
                jobUrl = linkElement.GetAttribute("href") ?? "";
                
                // Filter out non-job URLs
                if (!string.IsNullOrEmpty(jobUrl))
                {
                    string lowerUrl = jobUrl.ToLower();
                    string[] invalidUrlPatterns =
                    [
                        "/login", "/signup", "/browse", "/search", "/companies",
                        "over-130k", "trending-startups", "find-what", "hiring-now"
                    ];
                    
                    if (invalidUrlPatterns.Any(pattern => lowerUrl.Contains(pattern)))
                    {
                        jobUrl = ""; // Clear invalid URL
                    }
                }
            }
            catch (NoSuchElementException) 
            {
                try
                {
                    jobUrl = element.GetAttribute("href") ?? "";
                    
                    // Apply same filtering
                    if (!string.IsNullOrEmpty(jobUrl))
                    {
                        string lowerUrl = jobUrl.ToLower();
                        string[] invalidUrlPatterns =
                        [
                            "/login", "/signup", "/browse", "/search", "/companies",
                            "over-130k", "trending-startups", "find-what", "hiring-now"
                        ];
                        
                        if (invalidUrlPatterns.Any(pattern => lowerUrl.Contains(pattern)))
                        {
                            jobUrl = ""; // Clear invalid URL
                        }
                    }
                }
                catch (Exception) { }
            }
            
            // Extract location and summary from text content
            string[] lines = elementText.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                // Look for location indicators
                if (string.IsNullOrEmpty(location) && IsLocationText(trimmedLine))
                {
                    location = trimmedLine;
                }
                
                // Look for summary (longer descriptive text)
                if (string.IsNullOrEmpty(summary) && trimmedLine.Length is > 30 and < 200)
                {
                    summary = trimmedLine;
                }
            }
            
            // Fallback: use first line as title if none found
            if (string.IsNullOrEmpty(title) && lines.Length > 0)
            {
                title = lines[0];
            }
            
            // Check if this is actually a job vs navigation element
            if (!string.IsNullOrEmpty(title))
            {
                string lowerTitle = title.ToLower();
                string[] invalidTitlePatterns =
                [
                    "over 130k", "trending startups", "find what's next", "log in", "sign up",
                    "browse", "search jobs", "startup jobs", "remote jobs", "hiring now"
                ];
                
                if (invalidTitlePatterns.Any(pattern => lowerTitle.Contains(pattern)))
                {
                    return null; // Skip navigation elements
                }
            }
            
            // Fallback: use second line as company if none found
            if (string.IsNullOrEmpty(company) && lines.Length > 1)
            {
                company = lines[1];
            }
            
            if (string.IsNullOrEmpty(title)) return null;
            
            // Format URL
            string finalUrl;
            if (string.IsNullOrEmpty(jobUrl))
            {
                finalUrl = $"https://wellfound.com/jobs/{Uri.EscapeDataString(title.Replace(" ", "-").ToLower())}";
            }
            else
            {
                finalUrl = jobUrl.StartsWith("http") ? jobUrl : $"https://wellfound.com{jobUrl}";
            }
            
            var job = new EnhancedJobListing
            {
                Title = title,
                Company = string.IsNullOrEmpty(company) ? "Startup" : company,
                Location = string.IsNullOrEmpty(location) ? "Location Not Specified" : location,
                Summary = string.IsNullOrEmpty(summary) ? elementText.Substring(0, Math.Min(100, elementText.Length)) : summary,
                Url = finalUrl,
                DatePosted = DateTime.Now,
                RequiredSkills = ["Startup"]
            };
            
            job.IsRemote = IsRemoteJob(job.Location, job.Title, job.Summary);
            return job;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting job from AngelList element: {ex.Message}");
            return null;
        }
    }

    private async Task ExtractFromPageContent(List<EnhancedJobListing> jobs, int maxResults)
    {
        try
        {
            string pageSource = Driver.PageSource;
            Logger.LogInformation($"AngelList page source length: {pageSource.Length}");
            
            if (pageSource.Contains("Software Engineer") || 
                pageSource.Contains(".NET") || 
                pageSource.Contains("Developer") ||
                pageSource.Contains("startup"))
            {
                Logger.LogInformation("AngelList page contains job-related content");
                
                IWebElement bodyElement = Driver.FindElement(By.TagName("body"));
                string bodyText = bodyElement.Text;
                
                string[] lines = bodyText.Split('\n')
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToArray();
                
                for (var i = 0; i < lines.Length && jobs.Count < maxResults; i++)
                {
                    string line = lines[i];
                    
                    if (IsJobTitle(line) && line.Length is > 10 and < 150)
                    {
                        Logger.LogInformation($"Found potential AngelList job title: {line}");
                        
                        var job = new EnhancedJobListing
                        {
                            Title = line,
                            Company = ExtractCompanyFromContext(lines, i),
                            Location = ExtractLocationFromContext(lines, i),
                            Summary = ExtractSummaryFromContext(lines, i),
                            Url = $"https://wellfound.com/jobs/{Uri.EscapeDataString(line.Replace(" ", "-").ToLower())}",
                            DatePosted = DateTime.Now,
                            RequiredSkills = ["Startup"]
                        };
                        
                        job.IsRemote = IsRemoteJob(job.Location, job.Title, job.Summary);
                        jobs.Add(job);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting from AngelList page content: {ex.Message}");
        }
    }

    private static bool IsLocationText(string text)
    {
        string[] locationKeywords =
        [
            "remote", "hybrid", "on-site", "san francisco", "new york", "austin", "seattle", 
            "boston", "chicago", "los angeles", "atlanta", "denver", "portland"
        ];
        
        string lowerText = text.ToLower();
        return locationKeywords.Any(keyword => lowerText.Contains(keyword)) && text.Length < 50;
    }

    private static bool IsJobTitle(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 5) return false;
        
        string lowerText = text.ToLower();
        
        // Exclude common navigation/marketing text
        string[] excludePatterns =
        [
            "over 130k", "trending", "find what's next", "log in", "sign up", "get started",
            "browse", "search", "filter", "sort", "view all", "see more", "show more",
            "startup jobs", "remote jobs", "local jobs", "hiring now"
        ];
        
        if (excludePatterns.Any(pattern => lowerText.Contains(pattern)))
        {
            return false;
        }
        
        string[] jobKeywords =
        [
            "engineer", "developer", "programmer", "architect", "manager", "lead", "senior", "principal",
            "director", "specialist", "coordinator", "consultant", "founder", "cto", "ceo"
        ];
        
        string[] startupKeywords =
        [
            "founding", "early", "startup", "series", "equity"
        ];
        
        string[] techKeywords =
        [
            "software", "full stack", "backend", "frontend", "web", "api", "mobile",
            ".net", "c#", "javascript", "python", "react", "angular"
        ];
        
        bool hasJobKeyword = jobKeywords.Any(keyword => lowerText.Contains(keyword));
        bool hasStartupKeyword = startupKeywords.Any(keyword => lowerText.Contains(keyword));
        bool hasTechKeyword = techKeywords.Any(keyword => lowerText.Contains(keyword));
        
        return (hasJobKeyword || hasStartupKeyword) && (hasTechKeyword || hasStartupKeyword);
    }

    private static string ExtractCompanyFromContext(string[] lines, int jobTitleIndex)
    {
        for (int i = Math.Max(0, jobTitleIndex - 2); i < Math.Min(lines.Length, jobTitleIndex + 3); i++)
        {
            if (i == jobTitleIndex) continue;
            
            string line = lines[i].Trim();
            if (line.Length is > 2 and < 50 && 
                !IsJobTitle(line) && 
                !IsLocationText(line) &&
                !line.Contains("•") &&
                !line.Contains("$"))
            {
                return line;
            }
        }
        return "Startup";
    }

    private static string ExtractLocationFromContext(string[] lines, int jobTitleIndex)
    {
        for (int i = Math.Max(0, jobTitleIndex - 2); i < Math.Min(lines.Length, jobTitleIndex + 3); i++)
        {
            string line = lines[i].Trim();
            if (IsLocationText(line))
            {
                return line;
            }
        }
        return "Location Not Specified";
    }

    private static string ExtractSummaryFromContext(string[] lines, int jobTitleIndex)
    {
        for (int i = jobTitleIndex + 1; i < Math.Min(lines.Length, jobTitleIndex + 5); i++)
        {
            string line = lines[i].Trim();
            if (line.Length is > 30 and < 300 && 
                !IsJobTitle(line) && 
                !IsLocationText(line) &&
                line.Contains(" "))
            {
                return line;
            }
        }
        return "Startup opportunity - see job details for more information";
    }

    private static bool IsRemoteJob(string location, string title, string summary)
    {
        string text = $"{location} {title} {summary}".ToLower();
        return text.Contains("remote") || 
               text.Contains("work from home") || 
               text.Contains("telecommute") ||
               text.Contains("anywhere") ||
               text.Contains("distributed");
    }

    public override SiteConfiguration GetDefaultConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "AngelList",
            BaseUrl = "https://wellfound.com",
            SearchEndpoint = "/jobs",
            Selectors = new Dictionary<string, string>
            {
                ["jobCard"] = "[data-test='job-card'], .job-listing, .startup-job",
                ["title"] = "[data-test='job-title'], h3, h2, .job-title",
                ["company"] = "[data-test='company-name'], .company-name, [class*='company']",
                ["location"] = "[data-test='location'], .location, [class*='location']",
                ["summary"] = ".job-description, .description, p"
            },
            UrlParameters = new Dictionary<string, string>
            {
                ["q"] = "q",
                ["location"] = "location",
                ["remote"] = "remote"
            },
            RateLimit = new RateLimitConfig
            {
                RequestsPerMinute = 15,          // Conservative for startup site
                DelayBetweenRequests = 1500,     // Slightly longer delays
                RetryAttempts = 3,
                RetryDelay = 4000
            },
            AntiDetection = new AntiDetectionConfig
            {
                UserAgents =
                [
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36"
                ],
                RequiresCookieAccept = true,
                UsesCloudflare = false
            },
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
    }
}
