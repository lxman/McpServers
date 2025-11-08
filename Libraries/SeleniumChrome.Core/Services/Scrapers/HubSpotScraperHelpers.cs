using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services.Scrapers;

public partial class HubSpotScraper
{
    private Task<EnhancedJobListing?> ExtractJobDetails(IWebElement jobElement, IWebDriver driver)
    {
        try
        {
            // HubSpot-specific selectors (they use modern React components)
            var titleSelectors = new[] 
            { 
                ".job-title", 
                "h3 a", 
                "h2 a",
                "[data-testid='job-title']", 
                ".position-title", 
                "a[href*='job']",
                ".card-title",
                ".listing-title"
            };
            
            var locationSelectors = new[] 
            { 
                ".job-location", 
                ".location", 
                "[data-testid='location']", 
                ".office-location",
                ".workplace-type",
                ".job-location-text",
                ".remote-location"
            };
            
            var departmentSelectors = new[] 
            { 
                ".department", 
                ".team", 
                "[data-testid='department']", 
                ".job-category",
                ".functional-area",
                ".business-unit"
            };
            
            var descriptionSelectors = new[] 
            { 
                ".job-description", 
                ".description", 
                ".summary", 
                ".job-summary",
                ".job-excerpt",
                ".position-summary"
            };

            var title = ExtractTextUsingSelectorArray(jobElement, titleSelectors);
            var location = ExtractTextUsingSelectorArray(jobElement, locationSelectors);
            var department = ExtractTextUsingSelectorArray(jobElement, departmentSelectors);
            var description = ExtractTextUsingSelectorArray(jobElement, descriptionSelectors);

            // Get the job URL
            var jobUrl = "";
            try
            {
                var linkSelectors = new[]
                {
                    "a[href*='job']",
                    "a[href*='career']", 
                    "a[href*='position']",
                    ".job-title a",
                    "h3 a",
                    "h2 a",
                    ".apply-link",
                    "a[data-testid='job-link']"
                };

                foreach (var selector in linkSelectors)
                {
                    try
                    {
                        var linkElement = jobElement.FindElement(By.CssSelector(selector));
                        jobUrl = linkElement.GetAttribute("href") ?? "";
                        if (!string.IsNullOrWhiteSpace(jobUrl))
                            break;
                    }
                    catch
                    {
                    }
                }
                
                // Convert relative URLs to absolute
                if (jobUrl.StartsWith("/"))
                {
                    jobUrl = "https://www.hubspot.com" + jobUrl;
                }
            }
            catch
            {
                // If no direct link found, use the careers page
                jobUrl = "https://www.hubspot.com/careers/jobs";
            }

            // Skip if we don't have a title
            if (string.IsNullOrWhiteSpace(title))
            {
                return Task.FromResult<EnhancedJobListing?>(null);
            }

            // Extract additional job metadata
            var jobType = ExtractJobType(jobElement);
            var experience = ExtractExperienceLevel(title, description);

            var job = new EnhancedJobListing
            {
                Title = CleanText(title),
                Company = "HubSpot",
                Location = CleanText(location) ?? "Cambridge, MA",
                Description = CleanText(description) ?? "",
                Url = jobUrl,
                SourceSite = JobSite.HubSpot,
                DatePosted = DateTime.UtcNow,
                IsRemote = IsRemotePosition(location, description),
                Department = CleanText(department),
                JobType = jobType,
                ExperienceLevel = experience,
                Technologies = ExtractTechnologies(title + " " + description),
                MatchScore = CalculateMatchScore(title, description, department, [".NET", "C#", "ASP.NET", "Backend", "API"
                ])
            };

            return Task.FromResult(job);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error extracting job details from HubSpot element");
            return Task.FromResult<EnhancedJobListing?>(null);
        }
    }

    private static string? ExtractTextUsingSelectorArray(IWebElement parent, string[] selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var element = parent.FindElement(By.CssSelector(selector));
                var text = element?.Text?.Trim();
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

    private string ExtractJobType(IWebElement jobElement)
    {
        var jobTypeSelectors = new[] 
        { 
            ".job-type", 
            ".employment-type", 
            "[data-testid='job-type']",
            ".contract-type",
            ".position-type"
        };

        var jobTypeText = ExtractTextUsingSelectorArray(jobElement, jobTypeSelectors);
        
        if (!string.IsNullOrWhiteSpace(jobTypeText))
        {
            var lowerType = jobTypeText.ToLowerInvariant();
            if (lowerType.Contains("full time") || lowerType.Contains("full-time"))
                return "Full-time";
            if (lowerType.Contains("part time") || lowerType.Contains("part-time"))
                return "Part-time";
            if (lowerType.Contains("contract"))
                return "Contract";
            if (lowerType.Contains("intern"))
                return "Internship";
        }

        return "Full-time"; // Default assumption
    }

    private static string ExtractExperienceLevel(string? title, string? description)
    {
        var text = (title + " " + description).ToLowerInvariant();

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
            "software engineer", "api", "microservices", "azure", "sql server" 
        };
        
        var searchText = (job.Title + " " + job.Description + " " + job.Department).ToLowerInvariant();
        
        // High priority for .NET specific roles
        var hasNetKeywords = dotNetKeywords.Any(keyword => searchText.Contains(keyword));
        
        // Also include general software engineering roles at HubSpot (they often use .NET)
        var isSoftwareRole = searchText.Contains("software") && 
                             (searchText.Contains("engineer") || searchText.Contains("developer"));
        
        // Include backend and API roles (often .NET at HubSpot)
        var isBackendRole = searchText.Contains("backend") || searchText.Contains("api");
        
        return hasNetKeywords || isSoftwareRole || isBackendRole || job.MatchScore > 40;
    }

    private static List<string> ExtractTechnologies(string text)
    {
        var technologies = new List<string>();
        var techKeywords = new[]
        {
            // .NET ecosystem
            ".NET", "C#", "ASP.NET", ".NET Core", ".NET Framework", "Entity Framework",
            
            // Databases
            "SQL Server", "MySQL", "PostgreSQL", "Redis", "MongoDB",
            
            // Cloud & Infrastructure
            "Azure", "AWS", "Docker", "Kubernetes", "Microservices",
            
            // Frontend (HubSpot uses these)
            "React", "TypeScript", "JavaScript", "Node.js",
            
            // Tools & Frameworks
            "REST API", "GraphQL", "Git", "Jenkins", "CI/CD",
            
            // HubSpot specific technologies
            "HubL", "HubDB", "HubSpot CMS"
        };

        var lowerText = text.ToLowerInvariant();
        
        foreach (var tech in techKeywords)
        {
            if (lowerText.Contains(tech.ToLowerInvariant()))
            {
                technologies.Add(tech);
            }
        }

        return technologies.Distinct().ToList();
    }

    private static int CalculateMatchScore(string? title, string? description, string? department, string[] requiredKeywords)
    {
        var text = (title + " " + description + " " + department).ToLowerInvariant();
        var score = 0;

        // Base score for being at HubSpot (known to use .NET)
        score += 25;

        // Score for required keywords
        foreach (var keyword in requiredKeywords)
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

        // Bonus for backend/API roles (often .NET at HubSpot)
        if (text.Contains("backend") || text.Contains("api") || text.Contains("microservices"))
        {
            score += 15;
        }

        // Bonus for senior positions
        if (text.Contains("senior") || text.Contains("lead") || text.Contains("principal") || text.Contains("architect"))
        {
            score += 15;
        }

        // Bonus for remote work
        if (text.Contains("remote") || text.Contains("hybrid"))
        {
            score += 10;
        }

        // Bonus for engineering/product teams
        if (text.Contains("engineering") || text.Contains("product") || text.Contains("platform"))
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
        var text = (location + " " + description).ToLowerInvariant();
        
        var remoteKeywords = new[] 
        { 
            "remote", "work from home", "wfh", "distributed", "anywhere", 
            "telecommute", "home-based", "virtual", "fully remote", "remote-first" 
        };

        return remoteKeywords.Any(keyword => text.Contains(keyword));
    }
}