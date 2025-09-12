using System.Collections.ObjectModel;
using OpenQA.Selenium;
using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services.Scrapers;

public partial class StackOverflowScraper
{
    private async Task DismissModalsAsync(IWebDriver driver)
    {
        try
        {
            // Stack Overflow common modal/popup selectors
            var modalSelectors = new[]
            {
                // Cookie consent
                ".js-consent-banner button",
                ".consent-banner button",
                "[data-testid='cookie-banner'] button",
                ".onetrust-close-btn-ui",
                "#onetrust-accept-btn-handler",
                
                // General modals
                ".modal-close",
                ".popup-close",
                "[aria-label='Close']",
                ".close-button",
                
                // Stack Overflow specific
                ".js-modal-close",
                ".s-modal--close",
                ".js-dismissable"
            };

            foreach (string selector in modalSelectors)
            {
                try
                {
                    ReadOnlyCollection<IWebElement>? closeButtons = driver.FindElements(By.CssSelector(selector));
                    foreach (var closeButton in closeButtons)
                    {
                        if (closeButton is { Displayed: true, Enabled: true })
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

    private async Task<List<EnhancedJobListing>> ExtractJobListings(IWebDriver driver, EnhancedScrapeRequest request)
    {
        var jobs = new List<EnhancedJobListing>();

        try
        {
            // Stack Overflow job container selectors
            var jobContainerSelectors = new[]
            {
                ".job-listing",
                ".job-item",
                ".listResults .result",
                ".job-summary",
                ".job-card",
                "[data-jobid]",
                ".job"
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
                Logger.LogWarning("No job listings found on Stack Overflow careers page");
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
            Logger.LogError(ex, "Error extracting job listings from Stack Overflow");
        }

        return jobs;
    }

    private Task<EnhancedJobListing?> ExtractJobDetails(IWebElement jobElement, IWebDriver driver)
    {
        try
        {
            // Stack Overflow-specific selectors
            var titleSelectors = new[] 
            { 
                ".job-title a", 
                ".title a",
                "h2 a",
                ".job-link",
                "a[data-jobid]",
                ".job-summary .title"
            };
            
            var locationSelectors = new[] 
            { 
                ".job-location", 
                ".location", 
                ".job-info .location",
                ".remote-info"
            };
            
            var companySelectors = new[] 
            { 
                ".company", 
                ".employer",
                ".job-company",
                ".company-name"
            };
            
            var descriptionSelectors = new[] 
            { 
                ".job-description", 
                ".description", 
                ".job-summary",
                ".excerpt"
            };

            string? title = ExtractTextUsingSelectorArray(jobElement, titleSelectors);
            string? location = ExtractTextUsingSelectorArray(jobElement, locationSelectors);
            string company = ExtractTextUsingSelectorArray(jobElement, companySelectors) ?? "Stack Overflow Company";
            string? description = ExtractTextUsingSelectorArray(jobElement, descriptionSelectors);

            // Get the job URL
            var jobUrl = "";
            try
            {
                var linkSelectors = new[]
                {
                    ".job-title a",
                    ".title a",
                    "h2 a",
                    "a[data-jobid]",
                    ".job-link"
                };

                foreach (string selector in linkSelectors)
                {
                    try
                    {
                        IWebElement? linkElement = jobElement.FindElement(By.CssSelector(selector));
                        jobUrl = linkElement.GetAttribute("href") ?? "";
                        if (!string.IsNullOrWhiteSpace(jobUrl))
                            break;
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                // Convert relative URLs to absolute
                if (jobUrl.StartsWith("/"))
                {
                    jobUrl = "https://stackoverflow.co" + jobUrl;
                }
            }
            catch
            {
                // If no direct link found, use the work here page
                jobUrl = "https://stackoverflow.co/company/work-here/";
            }

            // Skip if we don't have a title
            if (string.IsNullOrWhiteSpace(title))
            {
                return Task.FromResult<EnhancedJobListing?>(null);
            }

            var job = new EnhancedJobListing
            {
                Title = CleanText(title),
                Company = CleanText(company),
                Location = CleanText(location) ?? "Remote",
                Description = CleanText(description) ?? "",
                Url = jobUrl,
                SourceSite = JobSite.StackOverflow,
                DatePosted = DateTime.UtcNow,
                IsRemote = IsRemotePosition(location, description),
                Department = "Engineering",
                JobType = "Full-time",
                ExperienceLevel = ExtractExperienceLevel(title, description),
                Technologies = ExtractTechnologies(title + " " + description),
                MatchScore = CalculateMatchScore(title, description, company, [".NET", "C#", "ASP.NET", "Backend", "Developer"
                ])
            };

            return Task.FromResult(job);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error extracting job details from Stack Overflow element");
            return Task.FromResult<EnhancedJobListing?>(null);
        }
    }

    private static string? ExtractTextUsingSelectorArray(IWebElement parent, string[] selectors)
    {
        foreach (string selector in selectors)
        {
            try
            {
                IWebElement? element = parent.FindElement(By.CssSelector(selector));
                string? text = element?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch
            {
                // Continue to next selector
            }
        }
        return null;
    }

    private static string ExtractExperienceLevel(string? title, string? description)
    {
        string text = (title + " " + description).ToLowerInvariant();

        if (text.Contains("senior") || text.Contains("sr.") || text.Contains("lead"))
            return "Senior";
        if (text.Contains("principal") || text.Contains("staff") || text.Contains("architect"))
            return "Principal";
        if (text.Contains("junior") || text.Contains("jr.") || text.Contains("entry"))
            return "Junior";
        if (text.Contains("mid-level") || text.Contains("intermediate"))
            return "Mid-level";

        return "Mid-level"; // Default assumption
    }

    private static bool IsRelevantJob(EnhancedJobListing job, EnhancedScrapeRequest request)
    {
        // Check if job contains .NET-related keywords
        var dotNetKeywords = new[] 
        { 
            ".net", "c#", "csharp", "asp.net", "dotnet", "backend", "full stack", 
            "software engineer", "api", "developer", "engineering" 
        };
        
        string searchText = (job.Title + " " + job.Description + " " + job.Company).ToLowerInvariant();
        
        // High priority for .NET specific roles
        bool hasNetKeywords = dotNetKeywords.Any(keyword => searchText.Contains(keyword));
        
        // Include general software engineering roles at Stack Overflow
        bool isSoftwareRole = searchText.Contains("software") && 
                              (searchText.Contains("engineer") || searchText.Contains("developer"));
        
        return hasNetKeywords || isSoftwareRole || job.MatchScore > 40;
    }

    private static List<string> ExtractTechnologies(string text)
    {
        var technologies = new List<string>();
        var techKeywords = new[]
        {
            // .NET ecosystem
            ".NET", "C#", "ASP.NET", ".NET Core", ".NET Framework", "Entity Framework",
            
            // Databases
            "SQL Server", "MySQL", "PostgreSQL", "Redis", "Elasticsearch",
            
            // Cloud & Infrastructure
            "Azure", "AWS", "Docker", "Kubernetes", "Microservices",
            
            // Web technologies
            "HTML", "CSS", "JavaScript", "TypeScript", "React",
            
            // Stack Overflow technologies
            "Stack Overflow", "Q&A Platform", "Community Platform"
        };

        string lowerText = text.ToLowerInvariant();
        
        foreach (string tech in techKeywords)
        {
            if (lowerText.Contains(tech.ToLowerInvariant()))
            {
                technologies.Add(tech);
            }
        }

        return technologies.Distinct().ToList();
    }

    private static int CalculateMatchScore(string? title, string? description, string? company, string[] requiredKeywords)
    {
        string text = (title + " " + description + " " + company).ToLowerInvariant();
        var score = 0;

        // Base score for being at Stack Overflow (developer-focused company)
        score += 30;

        // Score for required keywords
        foreach (string keyword in requiredKeywords)
        {
            if (text.Contains(keyword.ToLowerInvariant()))
            {
                score += 20;
            }
        }

        // Bonus for .NET specific mentions
        if (text.Contains(".net") || text.Contains("c#") || text.Contains("asp.net"))
        {
            score += 30;
        }

        // Bonus for developer/engineering roles
        if (text.Contains("developer") || text.Contains("engineer") || text.Contains("programming"))
        {
            score += 15;
        }

        // Bonus for senior positions
        if (text.Contains("senior") || text.Contains("lead") || text.Contains("principal"))
        {
            score += 15;
        }

        // Bonus for remote work
        if (text.Contains("remote") || text.Contains("distributed"))
        {
            score += 10;
        }

        return Math.Min(score, 100);
    }

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Trim()
            .Replace("\n", " ")
            .Replace("\r", "")
            .Replace("\t", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private static bool IsRemotePosition(string? location, string? description)
    {
        string text = (location + " " + description).ToLowerInvariant();
        
        var remoteKeywords = new[] 
        { 
            "remote", "work from home", "wfh", "distributed", "anywhere", 
            "telecommute", "home-based", "virtual", "fully remote", "remote-first" 
        };

        return remoteKeywords.Any(keyword => text.Contains(keyword));
    }
}
