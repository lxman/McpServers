using System.Text.Json;
using OpenQA.Selenium;
using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services.Scrapers;

/// <summary>
/// Streamlined SimplifyJobs service that uses direct API calls with job IDs
/// Replaces complex page scraping with simple, efficient API integration
/// </summary>
public class SimplifyJobsApiService : BaseJobScraper
{
    public override JobSite SupportedSite => JobSite.SimplifyJobs;

    public SimplifyJobsApiService(ILogger<SimplifyJobsApiService> logger) : base(logger) { }

    /// <summary>
    /// Fetch jobs by providing an array of job IDs extracted from web search results
    /// </summary>
    public async Task<List<EnhancedJobListing>> FetchJobsByIdsAsync(string[] jobIds, string userId = "default_user")
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            // Initialize minimal driver just for API calls
            InitializeDriver(new AntiDetectionConfig { RequiresLogin = true });
            
            // Ensure authentication for API access
            bool isAuthenticated = await EnsureAuthentication();
            if (!isAuthenticated)
            {
                Logger.LogError("Authentication failed. Cannot proceed with SimplifyJobs API calls.");
                return jobs;
            }
            
            Logger.LogInformation($"Processing {jobIds.Length} job IDs via SimplifyJobs API");
            
            // Process each job ID via direct API call
            for (var i = 0; i < jobIds.Length; i++)
            {
                string jobId = jobIds[i];
                Logger.LogInformation($"Processing job {i + 1}/{jobIds.Length}: {jobId}");
                
                try
                {
                    JsonElement? jobData = await FetchJobDetailsViaApi(jobId);
                    if (jobData.HasValue)
                    {
                        EnhancedJobListing? enhancedJob = ConvertToEnhancedJobListing(jobData.Value, userId);
                        if (enhancedJob != null)
                        {
                            enhancedJob.SourceSite = SupportedSite;
                            jobs.Add(enhancedJob);
                            Logger.LogInformation($"Success: {enhancedJob.Title} at {enhancedJob.Company}");
                            
                            // Log the external URL for verification
                            if (!string.IsNullOrEmpty(enhancedJob.Url) && !enhancedJob.Url.Contains("simplify.jobs"))
                            {
                                Logger.LogInformation($"External URL: {enhancedJob.Url}");
                            }
                        }
                    }
                    
                    // Respectful rate limiting
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to process job ID {jobId}: {ex.Message}");
                }
            }
            
            Logger.LogInformation($"Successfully processed {jobs.Count}/{jobIds.Length} jobs from SimplifyJobs API");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in SimplifyJobs API processing: {ex.Message}");
        }
        finally
        {
            Driver?.Quit();
            Driver?.Dispose();
        }
        
        return jobs;
    }

    /// <summary>
    /// Legacy method for compatibility
    /// </summary>
    public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        Logger.LogWarning("Legacy ScrapeJobsAsync called - use FetchJobsByIdsAsync instead.");
        return [];
    }

    /// <summary>
    /// Simple authentication check
    /// </summary>
    private async Task<bool> EnsureAuthentication()
    {
        try
        {
            Driver!.Navigate().GoToUrl("https://simplify.jobs");
            await Task.Delay(2000);
            
            Driver.Navigate().GoToUrl("https://simplify.jobs/jobs");
            await Task.Delay(1000);
            
            string currentUrl = Driver.Url.ToLower();
            bool isAuthenticated = currentUrl.Contains("/jobs") && !currentUrl.Contains("login");
            
            Logger.LogInformation($"Authentication status: {(isAuthenticated ? "Authenticated" : "May need login")}");
            return true; // Proceed anyway
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Authentication check failed: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Direct API call to SimplifyJobs
    /// </summary>
    private async Task<JsonElement?> FetchJobDetailsViaApi(string jobId)
    {
        try
        {
            Logger.LogInformation($"Fetching job details for ID: {jobId}");
            
            var apiUrl = $"https://api.simplify.jobs/v2/job-posting/:id/{jobId}/company";
            
            var jsScript = $@"
                var callback = arguments[arguments.length - 1];
                
                fetch('{apiUrl}', {{
                    method: 'GET',
                    credentials: 'include',
                    headers: {{
                        'Accept': 'application/json',
                        'Content-Type': 'application/json'
                    }}
                }})
                .then(response => {{
                    if (response.ok) {{
                        return response.json();
                    }} else {{
                        throw new Error('HTTP ' + response.status);
                    }}
                }})
                .then(data => {{
                    callback(JSON.stringify(data));
                }})
                .catch(error => {{
                    callback(null);
                }});
            ";
            
            object? result = await Task.Run(() => 
                ((IJavaScriptExecutor)Driver!).ExecuteAsyncScript(jsScript)
            );
            
            if (result is string jsonString && !string.IsNullOrEmpty(jsonString) && jsonString != "null")
            {
                try
                {
                    JsonDocument jsonDoc = JsonDocument.Parse(jsonString);
                    Logger.LogInformation($"Successfully fetched job data for {jobId}");
                    return jsonDoc.RootElement;
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning($"Invalid JSON response for {jobId}: {ex.Message}");
                }
            }
            else
            {
                Logger.LogWarning($"Empty response for job ID {jobId}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error fetching job details for {jobId}: {ex.Message}");
        }
        
        return null;
    }

    /// <summary>
    /// Convert API response to EnhancedJobListing
    /// </summary>
    private EnhancedJobListing? ConvertToEnhancedJobListing(JsonElement jobData, string userId)
    {
        try
        {
            // 🔍 DEBUG: Log the complete JSON structure
            Logger.LogInformation($"🔍 FULL API RESPONSE: {jobData.GetRawText()}");
            Logger.LogInformation($"🔍 Available properties: {string.Join(", ", jobData.EnumerateObject().Select(p => p.Name))}");
            
            if (!jobData.TryGetProperty("title", out JsonElement titleElement))
            {
                Logger.LogWarning("Missing title in job data");
                return null;
            }

            // Extract company name from nested job.company.name structure
            var companyName = "Unknown Company";
            
            // First try to get company from root level (legacy support)
            if (jobData.TryGetProperty("company", out JsonElement companyElement) &&
                companyElement.TryGetProperty("name", out JsonElement compNameElement))
            {
                companyName = compNameElement.GetString() ?? "Unknown Company";
                Logger.LogInformation($"Found company name at root level: {companyName}");
            }
            // Then try to get company from nested job.company.name structure
            else if (jobData.TryGetProperty("job", out JsonElement jobElement) &&
                     jobElement.TryGetProperty("company", out JsonElement nestedCompanyElement) &&
                     nestedCompanyElement.TryGetProperty("name", out JsonElement nestedCompNameElement))
            {
                companyName = nestedCompNameElement.GetString() ?? "Unknown Company";
                Logger.LogInformation($"Found company name in nested structure: {companyName}");
            }
            else
            {
                Logger.LogWarning("No company name found in either root or nested structure - using default");
            }
            
            var job = new EnhancedJobListing
            {
                Title = titleElement.GetString() ?? "Unknown Title",
                Company = companyName,
                SourceSite = SupportedSite,
                ScrapedAt = DateTime.UtcNow
            };

            // Extract the external application URL
            if (jobData.TryGetProperty("url", out JsonElement urlElement))
            {
                string? url = urlElement.GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    job.Url = url;
                    
                    if (!url.Contains("simplify.jobs"))
                    {
                        Logger.LogInformation($"External application URL: {url}");
                        job.Benefits = "Direct external application link";
                    }
                    else
                    {
                        job.Benefits = "Apply via SimplifyJobs";
                    }
                }
            }

            // Extract other fields
            if (jobData.TryGetProperty("description", out JsonElement descElement))
            {
                job.Description = descElement.GetString() ?? "";
            }

            // Extract location
            if (jobData.TryGetProperty("locations", out JsonElement locationsElement) && 
                locationsElement.ValueKind == JsonValueKind.Array)
            {
                JsonElement firstLocation = locationsElement.EnumerateArray().FirstOrDefault();
                if (firstLocation.TryGetProperty("value", out JsonElement locationValue))
                {
                    job.Location = locationValue.GetString();
                    job.IsRemote = job.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) ?? false;
                }
            }

            // Extract skills
            if (jobData.TryGetProperty("skills", out JsonElement skillsElement) && 
                skillsElement.ValueKind == JsonValueKind.Array)
            {
                job.Technologies = skillsElement.EnumerateArray()
                    .Where(s => s.TryGetProperty("name", out _))
                    .Select(s => s.GetProperty("name").GetString())
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Cast<string>()
                    .ToList();
            }

            // Basic match scoring
            job.MatchScore = CalculateBasicMatchScore(job);
            
            Logger.LogInformation($"Converted: {job.Title} at {job.Company}");
            return job;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error converting job data: {ex.Message}");
            return null;
        }
    }

    private static int CalculateBasicMatchScore(EnhancedJobListing job)
    {
        var score = 30;
        
        string title = job.Title?.ToLower() ?? "";
        string description = job.Description?.ToLower() ?? "";
        
        if (title.Contains(".net") || description.Contains(".net")) score += 25;
        if (title.Contains("c#") || description.Contains("c#")) score += 20;
        if (title.Contains("senior") || title.Contains("principal")) score += 15;
        if (job.IsRemote) score += 15;
        
        return Math.Min(score, 100);
    }

    public override SiteConfiguration GetDefaultConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "SimplifyJobs API",
            BaseUrl = "https://api.simplify.jobs/v2",
            RateLimit = new RateLimitConfig
            {
                DelayBetweenRequests = 1000,
                RequestsPerMinute = 30
            },
            AntiDetection = new AntiDetectionConfig
            {
                RequiresLogin = true,
                UsesCloudflare = true
            }
        };
    }
}
