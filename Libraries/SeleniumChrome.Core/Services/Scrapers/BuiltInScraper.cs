using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services.Scrapers;

public class BuiltInScraper(ILogger<BuiltInScraper> logger) : BaseJobScraper(logger)
{
    public override JobSite SupportedSite => JobSite.BuiltIn;

    public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            InitializeDriver(config.AntiDetection);
                
            string searchUrl = BuildSearchUrl(request, config);
            Logger.LogInformation($"Scraping BuiltIn: {searchUrl}");
            
            // Navigate to page
            Driver!.Navigate().GoToUrl(searchUrl);
            await Task.Delay(500); // Reduced from 2000ms
            
            Logger.LogInformation($"Page title: {Driver.Title}");
            Logger.LogInformation($"Page URL: {Driver.Url}");
            
            // Handle cookie consent (no screenshot in production for speed)
            await HandleCookieConsent();
            
            // Smart wait for job content instead of fixed delay
            await WaitForJobContent();
            
            // Find job elements with simple, reliable selectors
            var jobElements = new ReadOnlyCollection<IWebElement>(new List<IWebElement>());
            
            var jobSelectors = new[]
            {
                "a[href*='/job/']",              // Most reliable - direct job links
                ".job-card",                     // Common job card class
                "[data-cy='job-card']",          // BuiltIn specific
                "article",                       // Article elements
                "[class*='job']"                 // Any element with 'job' in class
            };
            
            // Try selectors in order
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
                Logger.LogInformation($"Processing {jobElements.Count} job elements");
                
                foreach (IWebElement element in jobElements.Take(request.MaxResults))
                {
                    try
                    {
                        EnhancedJobListing? job = ExtractJobFromElement(element);
                        if (job != null)
                        {
                            job.SourceSite = SupportedSite;
                            jobs.Add(job);
                            Logger.LogInformation($"Extracted job: {job.Title} at {job.Company} - URL: {job.Url}");
                        }
                        
                        await RespectRateLimit(config.RateLimit);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error extracting job from element: {ex.Message}");
                    }
                }
            }
            else
            {
                // Fallback: Extract from page text
                Logger.LogInformation("No job elements found, attempting text extraction");
                
                try
                {
                    string pageSource = Driver.PageSource;
                    Logger.LogInformation($"Page source length: {pageSource.Length}");
                    
                    if (pageSource.Contains("Software Engineer") || 
                        pageSource.Contains(".NET") || 
                        pageSource.Contains("Developer"))
                    {
                        Logger.LogInformation("Page contains job-related content");
                        
                        IWebElement bodyElement = Driver.FindElement(By.TagName("body"));
                        string bodyText = bodyElement.Text;
                        
                        List<EnhancedJobListing> textJobs = ExtractJobsFromText(bodyText, request.MaxResults);
                        jobs.AddRange(textJobs);
                        
                        Logger.LogInformation($"Extracted {textJobs.Count} jobs from text content");
                    }
                    else
                    {
                        Logger.LogWarning("Page does not contain expected job content");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Text extraction failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error scraping BuiltIn: {ex.Message}");
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
        
        Logger.LogInformation($"BuiltIn scraping completed. Found {jobs.Count} jobs");
        return jobs;
    }

    private async Task WaitForJobContent()
    {
        try
        {
            // Smart wait: Wait for either job elements to appear OR timeout after 2 seconds
            DateTime timeout = DateTime.Now.AddSeconds(2);
            var contentFound = false;
            
            while (DateTime.Now < timeout && !contentFound)
            {
                try
                {
                    // Check if any job-related content is available
                    ReadOnlyCollection<IWebElement> elements = Driver.FindElements(By.CssSelector("a[href*='/job/'], .job-card, article"));
                    if (elements.Count > 0)
                    {
                        contentFound = true;
                        Logger.LogInformation("Job content detected after smart wait");
                        break;
                    }
                    
                    // Also check for specific BuiltIn content patterns in page source
                    string pageSource = Driver.PageSource;
                    if (pageSource.Contains("Software Engineer") || pageSource.Contains("Developer") || pageSource.Contains("jobs"))
                    {
                        contentFound = true;
                        Logger.LogInformation("Job-related content detected in page source");
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
            await Task.Delay(300);
        }
    }

    private List<EnhancedJobListing> ExtractJobsFromText(string bodyText, int maxResults)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            string[] lines = bodyText.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToArray();
            
            Logger.LogInformation($"Analyzing {lines.Length} lines of text content");
            
            for (var i = 0; i < lines.Length && jobs.Count < maxResults; i++)
            {
                string line = lines[i];
                
                if (IsJobTitle(line) && line.Length is > 10 and < 150)
                {
                    Logger.LogInformation($"Found potential job title: {line}");
                    
                    var job = new EnhancedJobListing
                    {
                        Title = CleanJobTitle(line),
                        Company = ExtractCompanyFromContext(lines, i),
                        Location = ExtractLocationFromContext(lines, i),
                        Summary = ExtractSummaryFromContext(lines, i),
                        Url = $"https://builtin.com/jobs/{Uri.EscapeDataString(line.Replace(" ", "-").ToLower())}",
                        DatePosted = DateTime.Now,
                        RequiredSkills = ExtractSkillsFromContext(lines, i)
                    };
                    
                    job.IsRemote = IsRemoteJob(job.Location, job.Title, job.Summary);
                    
                    if (!string.IsNullOrEmpty(job.Title) && job.Title.Length > 5)
                    {
                        jobs.Add(job);
                        Logger.LogInformation($"Added job: {job.Title} at {job.Company}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting jobs from text: {ex.Message}");
        }
        
        return jobs;
    }

    private static string CleanJobTitle(string title)
    {
        return title
            .Replace("Job Title:", "")
            .Replace("Position:", "")
            .Replace("Role:", "")
            .Trim();
    }

    private static List<string> ExtractSkillsFromContext(string[] lines, int jobTitleIndex)
    {
        var skills = new List<string>();
        
        for (int i = Math.Max(0, jobTitleIndex - 3); i < Math.Min(lines.Length, jobTitleIndex + 5); i++)
        {
            string line = lines[i].ToLower();
            
            if (line.Contains(".net")) skills.Add(".NET");
            if (line.Contains("c#")) skills.Add("C#");
            if (line.Contains("angular")) skills.Add("Angular");
            if (line.Contains("azure")) skills.Add("Azure");
            if (line.Contains("aws")) skills.Add("AWS");
            if (line.Contains("sql server")) skills.Add("SQL Server");
            if (line.Contains("mongodb")) skills.Add("MongoDB");
            if (line.Contains("react")) skills.Add("React");
            if (line.Contains("blazor")) skills.Add("Blazor");
            if (line.Contains("entity framework")) skills.Add("Entity Framework");
        }
        
        return skills.Distinct().ToList();
    }

    private bool IsJobTitle(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 5) return false;
        
        string lowerText = text.ToLower();
        
        var jobKeywords = new[]
        {
            "engineer", "developer", "programmer", "architect", "manager", "lead", "senior", "principal",
            "director", "specialist", "coordinator", "consultant", "administrator", "technician",
            "analyst", "designer", "scientist", "officer"
        };
        
        var dotnetKeywords = new[]
        {
            ".net", "c#", "asp.net", "blazor", "entity framework", "mvc", "core", "framework"
        };
        
        var techKeywords = new[]
        {
            "software", "full stack", "backend", "frontend", "web", "api", "database", 
            "cloud", "azure", "aws", "devops", "qa", "test", "security"
        };
        
        bool hasJobKeyword = jobKeywords.Any(keyword => lowerText.Contains(keyword));
        bool hasDotnetKeyword = dotnetKeywords.Any(keyword => lowerText.Contains(keyword));
        bool hasTechKeyword = techKeywords.Any(keyword => lowerText.Contains(keyword));
        
        var excludePatterns = new[]
        {
            "ago", "posted", "apply", "save", "share", "view", "more", "less", "show", "hide",
            "filter", "sort", "search", "results", "jobs", "companies", "location", "salary",
            "benefits", "full-time", "part-time", "contract", "remote", "hybrid", "•"
        };
        
        bool hasExcludePattern = excludePatterns.Any(pattern => lowerText.Contains(pattern));
        
        bool isJobTitle = hasJobKeyword && !hasExcludePattern;
        
        if (isJobTitle && hasDotnetKeyword)
        {
            Logger.LogInformation($"Found .NET job title: {text}");
            return true;
        }
        
        if (isJobTitle && hasTechKeyword)
        {
            return true;
        }
        
        return isJobTitle && text.Length <= 100;
    }

    private string ExtractCompanyFromContext(string[] lines, int jobTitleIndex)
    {
        for (int i = Math.Max(0, jobTitleIndex - 2); i < Math.Min(lines.Length, jobTitleIndex + 3); i++)
        {
            if (i == jobTitleIndex) continue;
            
            string line = lines[i].Trim();
            if (line.Length is > 3 and < 50 && 
                !IsJobTitle(line) && 
                !line.Contains("Remote") && 
                !line.Contains("Full-time") &&
                !line.Contains("•"))
            {
                return line;
            }
        }
        return "Unknown Company";
    }

    private static string ExtractLocationFromContext(string[] lines, int jobTitleIndex)
    {
        for (int i = Math.Max(0, jobTitleIndex - 2); i < Math.Min(lines.Length, jobTitleIndex + 3); i++)
        {
            string line = lines[i].Trim();
            if (line.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("NY", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("CA", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("TX", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Boston", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Seattle", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Atlanta", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }
        return "Location Not Specified";
    }

    private string ExtractSummaryFromContext(string[] lines, int jobTitleIndex)
    {
        for (int i = jobTitleIndex + 1; i < Math.Min(lines.Length, jobTitleIndex + 5); i++)
        {
            string line = lines[i].Trim();
            if (line.Length is > 30 and < 200 && 
                !IsJobTitle(line) && 
                line.Contains(" "))
            {
                return line;
            }
        }
        return "Job description not available";
    }

    private async Task HandleCookieConsent()
    {
        try
        {
            await Task.Delay(500); // Reduced from 1000ms
            
            var acceptButtons = new[]
            {
                "Accept all",
                "Accept All", 
                "Accept",
                "button[data-testid='cookie-accept']",
                "button[id*='accept']",
                "[data-cy='accept-all-button']",
                ".cookie-consent button"
            };

            foreach (string buttonSelector in acceptButtons)
            {
                try
                {
                    IWebElement? acceptButton;
                    if (buttonSelector.Contains("[") || buttonSelector.Contains("button") || buttonSelector.Contains("."))
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
                        await Task.Delay(200); // Reduced from 500ms
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

    private async Task TakeDebugScreenshot(string context)
    {
        try
        {
            Screenshot screenshot = ((ITakesScreenshot)Driver!).GetScreenshot();
            var fileName = $"debug_{context}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine("Screenshots", fileName);
            screenshot.SaveAsFile(filePath);
            Logger.LogInformation($"Debug screenshot saved: {filePath}");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to take debug screenshot: {ex.Message}");
        }
    }

    private static string BuildSearchUrl(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        string baseUrl = config.BaseUrl; // https://builtin.com
        
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            if (request.SearchTerm.Contains(".NET", StringComparison.OrdinalIgnoreCase) ||
                request.SearchTerm.Contains("C#", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return "https://builtin.com/jobs/remote/dev-engineering";
                }

                return "https://builtin.com/jobs/dev-engineering";
            }
            
            string searchTerm = Uri.EscapeDataString(request.SearchTerm);
            string location = !string.IsNullOrEmpty(request.Location) ? 
                Uri.EscapeDataString(request.Location) : "";
            
            if (!string.IsNullOrEmpty(location))
            {
                return $"{baseUrl}/jobs?q={searchTerm}&location={location}";
            }

            return $"{baseUrl}/jobs?q={searchTerm}";
        }
        
        return $"{baseUrl}/jobs";
    }

    private EnhancedJobListing? ExtractJobFromElement(IWebElement element)
    {
        try
        {
            string elementText = element.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(elementText)) return null;
            
            string[] lines = elementText.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            if (lines.Length == 0) return null;
            
            var title = "";
            var company = "";
            var location = "";
            var summary = "";
            var jobUrl = "";
            
            // Extract basic info from text
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(title) && IsJobTitle(trimmedLine))
                {
                    title = trimmedLine;
                }
                else if (!string.IsNullOrEmpty(title) && string.IsNullOrEmpty(company) && 
                         trimmedLine.Length is > 3 and < 50)
                {
                    company = trimmedLine;
                }
                else if (trimmedLine.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                        trimmedLine.Length < 30 && (trimmedLine.Contains("NY") || trimmedLine.Contains("CA")))
                {
                    location = trimmedLine;
                }
                else if (trimmedLine.Length > 30 && string.IsNullOrEmpty(summary))
                {
                    summary = trimmedLine;
                }
            }
            
            if (string.IsNullOrEmpty(title)) return null;
            
            // Try to extract URL from element
            try
            {
                IWebElement linkElement = element.FindElement(By.TagName("a"));
                jobUrl = linkElement.GetAttribute("href") ?? "";
                Logger.LogInformation($"Raw URL extracted: '{jobUrl}'");
            }
            catch (NoSuchElementException) 
            {
                // If element IS a link, try to get href directly
                try
                {
                    jobUrl = element.GetAttribute("href") ?? "";
                    Logger.LogInformation($"Direct href extracted: '{jobUrl}'");
                }
                catch (Exception)
                {
                    Logger.LogWarning("No link found in element");
                }
            }
            
            // Format the final URL
            string finalUrl;
            if (string.IsNullOrEmpty(jobUrl))
            {
                finalUrl = $"https://builtin.com/jobs/{Uri.EscapeDataString(title.Replace(" ", "-").ToLower())}";
                Logger.LogInformation($"Generated URL: '{finalUrl}'");
            }
            else
            {
                finalUrl = jobUrl.StartsWith("http") ? jobUrl : $"https://builtin.com{jobUrl}";
                Logger.LogInformation($"Final formatted URL: '{finalUrl}'");
            }
            
            var job = new EnhancedJobListing
            {
                Title = title,
                Company = string.IsNullOrEmpty(company) ? "Company Not Listed" : company,
                Location = string.IsNullOrEmpty(location) ? "Location Not Specified" : location,
                Summary = string.IsNullOrEmpty(summary) ? elementText.Substring(0, Math.Min(100, elementText.Length)) : summary,
                Url = finalUrl,
                DatePosted = DateTime.Now,
                RequiredSkills = []
            };
            
            job.IsRemote = IsRemoteJob(job.Location, job.Title, job.Summary);
            return job;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting job from element: {ex.Message}");
            return null;
        }
    }

    private static bool IsRemoteJob(string location, string title, string summary)
    {
        string text = $"{location} {title} {summary}".ToLower();
        return text.Contains("remote") || 
               text.Contains("work from home") || 
               text.Contains("telecommute") ||
               text.Contains("hybrid") ||
               text.Contains("anywhere");
    }

    public override SiteConfiguration GetDefaultConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "BuiltIn",
            BaseUrl = "https://builtin.com",
            SearchEndpoint = "/jobs",
            Selectors = new Dictionary<string, string>
            {
                ["jobCard"] = "a[href*='/job/'], .job-card, article",
                ["title"] = "h3, h2, .job-title, a[href*='/jobs/']",
                ["company"] = ".company, [class*='company']",
                ["location"] = ".location, [class*='location']",
                ["summary"] = ".description, .summary, p"
            },
            UrlParameters = new Dictionary<string, string>
            {
                ["q"] = "q", 
                ["location"] = "location"
            },
            RateLimit = new RateLimitConfig
            {
                RequestsPerMinute = 20,          // Increased from 15
                DelayBetweenRequests = 1000,     // Reduced from 3000ms to 1000ms  
                RetryAttempts = 2,
                RetryDelay = 3000                // Reduced from 5000ms
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
