using System.Net;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services;

public class EmailJobAlertService(ILogger<EmailJobAlertService> logger, IConfiguration configuration)
{
    private readonly string _imapServer = configuration["EmailSettings:ImapServer"] ?? "";
    private readonly int _imapPort = configuration.GetValue("EmailSettings:ImapPort", 993);
    private readonly string _username = configuration["EmailSettings:Username"] ?? "";
    private readonly string _password = configuration["EmailSettings:Password"] ?? "";

    public async Task<List<EnhancedJobListing>> GetJobAlertsAsync(int daysBack = 7)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            using var client = new ImapClient();
            
            // Connect to IMAP server
            await client.ConnectAsync(_imapServer, _imapPort, true);
            await client.AuthenticateAsync(_username, _password);
            
            // Open INBOX
            await client.Inbox.OpenAsync(FolderAccess.ReadOnly);
            
            // Search for recent job alert emails
            BinarySearchQuery? searchQuery = SearchQuery.And(
                SearchQuery.DeliveredAfter(DateTime.Now.AddDays(-daysBack)),
                SearchQuery.Or(
                    SearchQuery.Or(
                        SearchQuery.Or(
                            SearchQuery.FromContains("linkedin.com"),
                            SearchQuery.FromContains("noreply@linkedin.com")
                        ),
                        SearchQuery.Or(
                            SearchQuery.FromContains("glassdoor.com"),
                            SearchQuery.FromContains("noreply@glassdoor.com")
                        )
                    ),
                    SearchQuery.Or(
                        SearchQuery.Or(
                            SearchQuery.FromContains("dice.com"),
                            SearchQuery.FromContains("noreply@dice.com")
                        ),
                        SearchQuery.Or(
                            SearchQuery.FromContains("indeed.com"),
                            SearchQuery.FromContains("noreply@indeed.com")
                        )
                    )
                )
            );
            
            IList<UniqueId>? messageIds = await client.Inbox.SearchAsync(searchQuery);
            logger.LogInformation($"Found {messageIds.Count} job alert emails in last {daysBack} days");
            
            // Add debug logging to see what emails we're finding
            if (messageIds.Count == 0)
            {
                logger.LogWarning($"No emails found matching job alert criteria in last {daysBack} days");
                
                // Try a broader search to see if there are any emails at all
                IList<UniqueId>? recentEmails = await client.Inbox.SearchAsync(SearchQuery.DeliveredAfter(DateTime.Now.AddDays(-daysBack)));
                logger.LogInformation($"Total emails in last {daysBack} days: {recentEmails.Count}");
                
                // Sample a few recent emails to see their senders
                foreach (UniqueId id in recentEmails.Take(5))
                {
                    try
                    {
                        MimeMessage? msg = await client.Inbox.GetMessageAsync(id);
                        logger.LogInformation($"Recent email from: {msg.From.FirstOrDefault()}, Subject: {msg.Subject}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error checking recent email: {ex.Message}");
                    }
                }
            }
            
            foreach (UniqueId messageId in messageIds.Take(50)) // Limit to recent 50 emails
            {
                try
                {
                    MimeMessage? message = await client.Inbox.GetMessageAsync(messageId);
                    List<EnhancedJobListing> parsedJobs = await ParseJobAlertEmail(message);
                    jobs.AddRange(parsedJobs);
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Error parsing email {messageId}: {ex.Message}");
                }
            }
            
            await client.DisconnectAsync(true);
            
            logger.LogInformation($"Successfully parsed {jobs.Count} jobs from email alerts");
            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error retrieving job alert emails: {ex.Message}");
            return jobs;
        }
    }

    private async Task<List<EnhancedJobListing>> ParseJobAlertEmail(MimeMessage message)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            string sender = message.From.FirstOrDefault()?.ToString().ToLower() ?? "";
            string subject = message.Subject ?? "";
            string body = message.TextBody ?? message.HtmlBody ?? "";
            
            logger.LogDebug($"Processing email from: {sender}, Subject: {subject}");
            
            // Determine email source and parse accordingly
            if (sender.Contains("linkedin"))
            {
                jobs.AddRange(await ParseLinkedInJobAlert(subject, body, message.Date.DateTime));
            }
            else if (sender.Contains("glassdoor"))
            {
                jobs.AddRange(await ParseGlassdoorJobAlert(subject, body, message.Date.DateTime));
            }
            else if (sender.Contains("dice"))
            {
                jobs.AddRange(await ParseDiceJobAlert(subject, body, message.Date.DateTime));
            }
            else if (sender.Contains("indeed"))
            {
                jobs.AddRange(await ParseIndeedJobAlert(subject, body, message.Date.DateTime));
            }
            
            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Error parsing email: {ex.Message}");
            return jobs;
        }
    }

    private async Task<List<EnhancedJobListing>> ParseLinkedInJobAlert(string subject, string body, DateTime emailDate)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            // LinkedIn job alert patterns
            // Example: "5 new jobs for Senior .NET Developer"
            // Body contains job listings with titles, companies, locations
            
            MatchCollection jobMatches = Regex.Matches(body, 
                @"<a[^>]*href=""([^""]*linkedin\.com/jobs/view/[^""]*)"">([^<]+)</a>.*?<td[^>]*>([^<]+)</td>.*?<td[^>]*>([^<]+)</td>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match match in jobMatches)
            {
                var job = new EnhancedJobListing
                {
                    Title = CleanText(match.Groups[2].Value),
                    Company = CleanText(match.Groups[3].Value),
                    Location = CleanText(match.Groups[4].Value),
                    Url = match.Groups[1].Value,
                    SourceSite = JobSite.LinkedIn,
                    ScrapedAt = DateTime.UtcNow,
                    DatePosted = emailDate,
                    Notes = $"From LinkedIn email alert: {subject}"
                };
                
                // Determine if remote
                job.IsRemote = DetermineRemoteStatus(job.Location, job.Title);
                job.MatchScore = CalculateEmailJobMatchScore(job);
                
                jobs.Add(job);
            }
            
            logger.LogDebug($"Parsed {jobs.Count} jobs from LinkedIn email alert");
            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Error parsing LinkedIn email: {ex.Message}");
            return jobs;
        }
    }

    private async Task<List<EnhancedJobListing>> ParseGlassdoorJobAlert(string subject, string body, DateTime emailDate)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            logger.LogInformation($"Parsing Glassdoor email with subject: {subject}");
            logger.LogDebug($"Email body length: {body.Length} characters");
            
            // Multiple patterns to try for Glassdoor emails
            var patterns = new[]
            {
                // Pattern 1: Standard job alert with links
                @"<a[^>]*href=""([^""]*glassdoor\.com[^""]*job[^""]*)"">([^<]+)</a>.*?<div[^>]*>([^<]+)</div>.*?<div[^>]*>([^<]+)</div>",
                
                // Pattern 2: Simplified pattern focusing on job URLs
                @"<a[^>]*href=""([^""]*glassdoor\.com[^""]*job[^""]*)"">([^<]+)</a>",
                
                // Pattern 3: Look for any glassdoor job links with surrounding text
                @"https://www\.glassdoor\.com/job-listing/([^""'\s]+)",
                
                // Pattern 4: Alternative link format
                @"href=""([^""]*glassdoor\.com[^""]*)"">([^<]+)</a>"
            };
            
            foreach (string pattern in patterns)
            {
                logger.LogDebug($"Trying pattern: {pattern}");
                
                MatchCollection jobMatches = Regex.Matches(body,
                    pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                logger.LogInformation($"Pattern found {jobMatches.Count} matches");
                
                if (jobMatches.Count > 0)
                {
                    foreach (Match match in jobMatches)
                    {
                        var job = new EnhancedJobListing
                        {
                            Title = match.Groups.Count > 2 ? CleanText(match.Groups[2].Value) : "Job Title Not Extracted",
                            Company = match.Groups.Count > 3 ? CleanText(match.Groups[3].Value) : "Company Not Extracted",
                            Location = match.Groups.Count > 4 ? CleanText(match.Groups[4].Value) : "Location Not Extracted",
                            Url = match.Groups[1].Value,
                            SourceSite = JobSite.Glassdoor,
                            ScrapedAt = DateTime.UtcNow,
                            DatePosted = emailDate,
                            Notes = $"From Glassdoor email alert: {subject}"
                        };
                        
                        // Extract salary if present in email
                        Match salaryMatch = Regex.Match(body, 
                            @"\$[\d,]+(?:\s*-\s*\$[\d,]+)?", RegexOptions.IgnoreCase);
                        if (salaryMatch.Success)
                        {
                            job.Salary = salaryMatch.Value;
                        }
                        
                        job.IsRemote = DetermineRemoteStatus(job.Location, job.Title);
                        job.MatchScore = CalculateEmailJobMatchScore(job);
                        
                        jobs.Add(job);
                        logger.LogInformation($"Extracted job: {job.Title} at {job.Company}");
                    }
                    
                    break; // Stop trying patterns once we find matches
                }
            }
            
            // If we found jobs, try to enhance them with detailed information
            if (jobs.Count > 0)
            {
                logger.LogInformation($"Attempting to enhance {jobs.Count} Glassdoor jobs with detailed information");
                for (var i = 0; i < jobs.Count; i++)
                {
                    try
                    {
                        jobs[i] = await ScrapeGlassdoorJobDetails(jobs[i]);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Failed to enhance job {jobs[i].Title}: {ex.Message}");
                        // Continue with original job if enhancement fails
                    }
                }
            }
            
            // If no jobs found, log the email content for debugging
            if (jobs.Count == 0)
            {
                logger.LogWarning("No jobs extracted from Glassdoor email");
                logger.LogDebug($"Email subject: {subject}");
                
                // Log first 500 characters of body for debugging (be careful with sensitive info)
                string bodyPreview = body.Length > 500 ? body.Substring(0, 500) + "..." : body;
                logger.LogDebug($"Email body preview: {bodyPreview}");
                
                // Check if email contains any job-related keywords
                var jobKeywords = new[] { "job", "position", "opportunity", "hiring", "career", "apply" };
                List<string> foundKeywords = jobKeywords.Where(keyword => body.ToLower().Contains(keyword)).ToList();
                logger.LogInformation($"Job-related keywords found: {string.Join(", ", foundKeywords)}");
            }
            
            logger.LogInformation($"Successfully parsed {jobs.Count} jobs from Glassdoor email alert");
            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error parsing Glassdoor email: {ex.Message}");
            return jobs;
        }
    }

    private async Task<List<EnhancedJobListing>> ParseDiceJobAlert(string subject, string body, DateTime emailDate)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            // Dice job alert patterns - tech focused
            MatchCollection jobMatches = Regex.Matches(body,
                @"<a[^>]*href=""([^""]*dice\.com[^""]*)"">([^<]+)</a>.*?<span[^>]*>([^<]+)</span>.*?<span[^>]*>([^<]+)</span>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match match in jobMatches)
            {
                var job = new EnhancedJobListing
                {
                    Title = CleanText(match.Groups[2].Value),
                    Company = CleanText(match.Groups[3].Value),
                    Location = CleanText(match.Groups[4].Value),
                    Url = match.Groups[1].Value,
                    SourceSite = JobSite.Dice,
                    ScrapedAt = DateTime.UtcNow,
                    DatePosted = emailDate,
                    Notes = $"From Dice email alert: {subject}"
                };
                
                job.IsRemote = DetermineRemoteStatus(job.Location, job.Title);
                job.MatchScore = CalculateEmailJobMatchScore(job);
                
                jobs.Add(job);
            }
            
            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Error parsing Dice email: {ex.Message}");
            return jobs;
        }
    }

    private async Task<List<EnhancedJobListing>> ParseIndeedJobAlert(string subject, string body, DateTime emailDate)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            MatchCollection jobMatches = Regex.Matches(body,
                @"<a[^>]*href=""([^""]*indeed\.com[^""]*)"">([^<]+)</a>.*?<span[^>]*>([^<]+)</span>.*?<span[^>]*>([^<]+)</span>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match match in jobMatches)
            {
                var job = new EnhancedJobListing
                {
                    Title = CleanText(match.Groups[2].Value),
                    Company = CleanText(match.Groups[3].Value),
                    Location = CleanText(match.Groups[4].Value),
                    Url = match.Groups[1].Value,
                    SourceSite = JobSite.Indeed,
                    ScrapedAt = DateTime.UtcNow,
                    DatePosted = emailDate,
                    Notes = $"From Indeed email alert: {subject}"
                };
                
                job.IsRemote = DetermineRemoteStatus(job.Location, job.Title);
                job.MatchScore = CalculateEmailJobMatchScore(job);
                
                jobs.Add(job);
            }
            
            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Error parsing Indeed email: {ex.Message}");
            return jobs;
        }
    }
    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Remove HTML tags
        text = Regex.Replace(text, @"<[^>]*>", "");
        
        // Decode HTML entities
        text = WebUtility.HtmlDecode(text);
        
        // Clean up whitespace
        text = Regex.Replace(text, @"\s+", " ");
        
        return text.Trim();
    }

    private bool DetermineRemoteStatus(string location, string title)
    {
        var remoteKeywords = new[] { 
            "remote", "work from home", "wfh", "telecommute", "distributed", 
            "home office", "anywhere", "virtual", "telework", "remote work",
            "work remotely", "remote position", "remote opportunity"
        };
        
        string combinedText = $"{location} {title}".ToLower();
        return remoteKeywords.Any(keyword => combinedText.Contains(keyword));
    }

    private double CalculateEmailJobMatchScore(EnhancedJobListing job)
    {
        double score = 0;
        string jobText = $"{job.Title} {job.Company} {job.Location}".ToLower();
        
        // .NET stack preferences (high weight for email alerts)
        var dotnetKeywords = new[] { ".net", "c#", "csharp", "dotnet", "asp.net" };
        foreach (string keyword in dotnetKeywords)
        {
            if (jobText.Contains(keyword))
                score += 15; // Higher weight for email alerts
        }
        
        // Database experience
        var dbKeywords = new[] { "sql server", "mongodb", "database", "entity framework" };
        foreach (string keyword in dbKeywords)
        {
            if (jobText.Contains(keyword))
                score += 10;
        }
        
        // Remote work preference
        if (job.IsRemote)
            score += 20;
        
        // Senior level positions
        var seniorKeywords = new[] { "senior", "lead", "principal", "architect" };
        if (seniorKeywords.Any(keyword => jobText.Contains(keyword)))
            score += 15;
        
        // Startup indicators
        var startupKeywords = new[] { "startup", "series a", "series b", "growth company" };
        if (startupKeywords.Any(keyword => jobText.Contains(keyword)))
            score += 10;
        
        return Math.Min(score, 100);
    }

    /// <summary>
    /// Enhance job listings by scraping detailed information from their URLs
    /// </summary>
    public async Task<List<EnhancedJobListing>> EnhanceJobsWithDetails(List<EnhancedJobListing> jobs)
    {
        var enhancedJobs = new List<EnhancedJobListing>();
        
        foreach (EnhancedJobListing job in jobs)
        {
            try
            {
                if (job.SourceSite == JobSite.Glassdoor && !string.IsNullOrEmpty(job.Url))
                {
                    EnhancedJobListing enhancedJob = await ScrapeGlassdoorJobDetails(job);
                    enhancedJobs.Add(enhancedJob);
                }
                else
                {
                    enhancedJobs.Add(job); // Return original if no enhancement available
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to enhance job details for {job.Title}: {ex.Message}");
                enhancedJobs.Add(job); // Return original if enhancement fails
            }
        }
        
        return enhancedJobs;
    }
    
    /// <summary>
    /// Scrape detailed job information from a Glassdoor job URL
    /// </summary>
    private async Task<EnhancedJobListing> ScrapeGlassdoorJobDetails(EnhancedJobListing job)
    {
        try
        {
            logger.LogInformation($"Scraping details for job: {job.Title} from {job.Url}");
            
            // Extract job listing ID from the URL to construct a direct URL
            Match jobIdMatch = Regex.Match(job.Url, @"jobListingId=(\d+)");
            if (!jobIdMatch.Success)
            {
                logger.LogWarning($"Could not extract job ID from URL: {job.Url}");
                return job;
            }
            
            string jobId = jobIdMatch.Groups[1].Value;
            
            // Try multiple URL formats that might work
            var urlsToTry = new[]
            {
                $"https://www.glassdoor.com/job-listing/{jobId}",
                $"https://www.glassdoor.com/Jobs/job/{jobId}",
                $"https://www.glassdoor.com/job-listing/job-{jobId}.htm"
            };
            
            using var httpClient = new HttpClient();
            
            // Set headers to mimic a real browser
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            
            string html = null;
            string workingUrl = null;
            
            // Try each URL format
            foreach (string url in urlsToTry)
            {
                try
                {
                    logger.LogDebug($"Trying URL: {url}");
                    html = await httpClient.GetStringAsync(url);
                    workingUrl = url;
                    logger.LogInformation($"Successfully accessed job page: {url}");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogDebug($"URL {url} failed: {ex.Message}");
                }
            }
            
            if (string.IsNullOrEmpty(html))
            {
                logger.LogWarning($"All URL attempts failed for job ID {jobId}");
                
                // Try to extract basic info from the original email-based title if available
                if (!string.IsNullOrEmpty(job.Title))
                {
                    // At least try to determine if it's remote from the title
                    job.IsRemote = DetermineRemoteStatus("", job.Title);
                    
                    // Give it a basic score based on title content
                    job.MatchScore = CalculateBasicMatchScore(job.Title);
                }
                
                return job;
            }
            
            // Update the URL to the working direct URL
            if (!string.IsNullOrEmpty(workingUrl))
            {
                job.Url = workingUrl;
            }
            
            // Extract company name using multiple patterns
            var companyPatterns = new[]
            {
                @"<span[^>]*class=""[^""]*employer[^""]*""[^>]*>([^<]+)</span>",
                @"""employer""[^>]*>([^<]+)<",
                @"<div[^>]*data-test=""employer-name""[^>]*>([^<]+)</div>",
                @"<a[^>]*href=""[^""]*company[^""]*""[^>]*>([^<]+)</a>",
                @"class=""css-[^""]*""[^>]*>([^<]+)</span>.*?company"
            };
            
            foreach (string pattern in companyPatterns)
            {
                Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    job.Company = CleanText(match.Groups[1].Value);
                    logger.LogDebug($"Extracted company: {job.Company}");
                    break;
                }
            }
            
            // Extract location using multiple patterns
            var locationPatterns = new[]
            {
                @"<div[^>]*data-test=""job-location""[^>]*>([^<]+)</div>",
                @"<span[^>]*class=""[^""]*location[^""]*""[^>]*>([^<]+)</span>",
                @"""location""[^>]*>([^<]+)<",
                @"<div[^>]*location[^>]*>([^<]+)</div>"
            };
            
            foreach (string pattern in locationPatterns)
            {
                Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    job.Location = CleanText(match.Groups[1].Value);
                    job.IsRemote = DetermineRemoteStatus(job.Location, job.Title);
                    logger.LogDebug($"Extracted location: {job.Location}");
                    break;
                }
            }
            
            // Extract job description
            var descriptionPatterns = new[]
            {
                @"<div[^>]*class=""[^""]*jobDescriptionContent[^""]*""[^>]*>(.*?)</div>",
                @"<section[^>]*data-test=""jobDescription""[^>]*>(.*?)</section>",
                @"<div[^>]*job-description[^>]*>(.*?)</div>"
            };
            
            foreach (string pattern in descriptionPatterns)
            {
                Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                {
                    string rawDescription = match.Groups[1].Value;
                    job.Description = CleanText(Regex.Replace(rawDescription, @"<[^>]+>", " "));
                    job.FullDescription = rawDescription;
                    logger.LogDebug($"Extracted description: {job.Description?.Substring(0, Math.Min(100, job.Description.Length))}...");
                    break;
                }
            }
            
            // Extract salary information
            var salaryPatterns = new[]
            {
                @"<span[^>]*class=""[^""]*salary[^""]*""[^>]*>([^<]+)</span>",
                @"\$[\d,]+(?:\s*-\s*\$[\d,]+)?(?:\s*(?:per|/)\s*(?:year|hour|yr|hr))?",
                @"salary[^>]*>([^<]*\$[^<]+)</[^>]*>"
            };
            
            foreach (string pattern in salaryPatterns)
            {
                Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    job.Salary = CleanText(match.Groups[1].Value ?? match.Value);
                    logger.LogDebug($"Extracted salary: {job.Salary}");
                    break;
                }
            }
            
            // Look for .NET/C# related skills and technologies
            MatchCollection techMatches = Regex.Matches(html,
                @"\b(\.NET|C#|ASP\.NET|Azure|SQL Server|Entity Framework|MVC|Web API|Blazor|MAUI|WPF|WinForms|Visual Studio|Microsoft|Angular|React|JavaScript|TypeScript)\b",
                RegexOptions.IgnoreCase);
            
            var technologies = new List<string>();
            foreach (Match match in techMatches)
            {
                string tech = match.Value;
                if (!technologies.Contains(tech, StringComparer.OrdinalIgnoreCase))
                {
                    technologies.Add(tech);
                }
            }
            job.Technologies = technologies;
            
            // Update match score based on enhanced details
            job.MatchScore = CalculateEnhancedMatchScore(job);
            
            logger.LogInformation($"Successfully enhanced job: {job.Title} at {job.Company} in {job.Location} (Score: {job.MatchScore})");
            
            return job;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error scraping Glassdoor job details: {ex.Message}");
            
            // Fallback: at least try to score the job based on title
            if (!string.IsNullOrEmpty(job.Title))
            {
                job.MatchScore = CalculateBasicMatchScore(job.Title);
            }
            
            return job; // Return original job if scraping fails
        }
    }
    
    /// <summary>
    /// Calculate basic match score based on just the job title
    /// </summary>
    private int CalculateBasicMatchScore(string title)
    {
        var score = 0;
        string lowerTitle = title.ToLower();
        
        if (lowerTitle.Contains(".net")) score += 30;
        if (lowerTitle.Contains("c#")) score += 30;
        if (lowerTitle.Contains("senior")) score += 15;
        if (lowerTitle.Contains("lead") || lowerTitle.Contains("architect")) score += 10;
        if (lowerTitle.Contains("remote")) score += 10;
        if (lowerTitle.Contains("software") || lowerTitle.Contains("developer") || lowerTitle.Contains("engineer")) score += 5;
        
        return Math.Min(score, 100);
    }
    
    /// <summary>
    /// Calculate match score for jobs with enhanced details
    /// </summary>
    private int CalculateEnhancedMatchScore(EnhancedJobListing job)
    {
        var score = 0;
        
        // Base score for .NET related keywords
        if (job.Title?.ToLower().Contains(".net") == true || job.Title?.ToLower().Contains("c#") == true)
            score += 30;
        
        if (job.Description?.ToLower().Contains(".net") == true)
            score += 20;
        
        if (job.Description?.ToLower().Contains("c#") == true)
            score += 20;
        
        // Technology stack scoring
        if (job.Technologies?.Any() == true)
        {
            foreach (string tech in job.Technologies)
            {
                switch (tech.ToLower())
                {
                    case ".net":
                    case "c#":
                        score += 15;
                        break;
                    case "asp.net":
                    case "entity framework":
                        score += 10;
                        break;
                    case "azure":
                    case "sql server":
                        score += 8;
                        break;
                    default:
                        score += 5;
                        break;
                }
            }
        }
        
        // Remote work bonus
        if (job.IsRemote)
            score += 15;
        
        // Senior level bonus
        if (job.Title?.ToLower().Contains("senior") == true)
            score += 10;
        
        // Cap the score at 100
        return Math.Min(score, 100);
    }

    public async Task<List<EnhancedJobListing>> GetRecentJobAlertsAsync()
    {
        return await GetJobAlertsAsync(3); // Last 3 days
    }

    public async Task<List<EnhancedJobListing>> GetLinkedInJobAlertsAsync(int daysBack = 7)
    {
        List<EnhancedJobListing> allJobs = await GetJobAlertsAsync(daysBack);
        return allJobs.Where(j => j.SourceSite == JobSite.LinkedIn).ToList();
    }

    public async Task<List<EnhancedJobListing>> GetGlassdoorJobAlertsAsync(int daysBack = 7)
    {
        List<EnhancedJobListing> allJobs = await GetJobAlertsAsync(daysBack);
        return allJobs.Where(j => j.SourceSite == JobSite.Glassdoor).ToList();
    }

    public async Task<EmailJobAlertSummary> GetJobAlertSummaryAsync(int daysBack = 7)
    {
        List<EnhancedJobListing> jobs = await GetJobAlertsAsync(daysBack);
        
        return new EmailJobAlertSummary
        {
            TotalJobs = jobs.Count,
            LinkedInJobs = jobs.Count(j => j.SourceSite == JobSite.LinkedIn),
            GlassdoorJobs = jobs.Count(j => j.SourceSite == JobSite.Glassdoor),
            DiceJobs = jobs.Count(j => j.SourceSite == JobSite.Dice),
            IndeedJobs = jobs.Count(j => j.SourceSite == JobSite.Indeed),
            RemoteJobs = jobs.Count(j => j.IsRemote),
            HighMatchJobs = jobs.Count(j => j.MatchScore > 70),
            DaysAnalyzed = daysBack,
            LastUpdated = DateTime.UtcNow
        };
    }
}

public class EmailJobAlertSummary
{
    public int TotalJobs { get; set; }
    public int LinkedInJobs { get; set; }
    public int GlassdoorJobs { get; set; }
    public int DiceJobs { get; set; }
    public int IndeedJobs { get; set; }
    public int RemoteJobs { get; set; }
    public int HighMatchJobs { get; set; }
    public int DaysAnalyzed { get; set; }
    public DateTime LastUpdated { get; set; }
}