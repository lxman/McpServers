using SeleniumChromeTool.Models;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace SeleniumChromeTool.Services.Scrapers
{
    /// <summary>
    /// Google Custom Search API-based SimplifyJobs discovery service that implements the proven working method:
    /// 1. Google Custom Search API: site:simplify.jobs {search_term}
    /// 2. Extract job URLs from search results
    /// 3. Parse job IDs from URLs
    /// 4. Use SimplifyJobsApiService to fetch detailed job data
    /// 
    /// UPDATED: Now uses Google Custom Search API instead of web scraping
    /// </summary>
    public class GoogleSimplifyJobsService : BaseJobScraper
    {
        private readonly SimplifyJobsApiService _apiService;
        private readonly HttpClient _httpClient;
        
        // Google Custom Search API configuration
        // TODO: Move these to appsettings.json or environment variables for production
        private const string GOOGLE_API_KEY = ""; // Replace this with your actual API key
        private const string SEARCH_ENGINE_ID = ""; // Replace this with your actual Search Engine ID (cx)
        private const string CUSTOM_SEARCH_API_URL = "https://www.googleapis.com/customsearch/v1";
        
        public override JobSite SupportedSite => JobSite.SimplifyJobs;
        
        // Regex pattern to extract job IDs from SimplifyJobs URLs
        // Pattern: https://simplify.jobs/p/{JOB_ID}/{JOB_TITLE}
        private static readonly Regex JobIdRegex = new(@"/p/([a-f0-9-]{36})/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public GoogleSimplifyJobsService(ILogger<GoogleSimplifyJobsService> logger, SimplifyJobsApiService apiService, HttpClient httpClient) : base(logger)
        {
            _apiService = apiService;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Required by BaseJobScraper - routes to Google Custom Search API discovery
        /// </summary>
        public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
        {
            return await DiscoverAndFetchJobsAsync(request);
        }

        /// <summary>
        /// Required by BaseJobScraper - provides default configuration
        /// </summary>
        public override SiteConfiguration GetDefaultConfiguration()
        {
            return new SiteConfiguration
            {
                SiteName = "SimplifyJobs",
                BaseUrl = "https://simplify.jobs",
                AntiDetection = new AntiDetectionConfig
                {
                    RequiresLogin = false
                }
            };
        }

        /// <summary>
        /// Discover and fetch SimplifyJobs using Google Custom Search API
        /// </summary>
        public async Task<List<EnhancedJobListing>> DiscoverAndFetchJobsAsync(EnhancedScrapeRequest request)
        {
            try
            {
                Logger.LogInformation($"🔍 Starting Google Custom Search API SimplifyJobs discovery for: '{request.SearchTerm}'");
                
                // Step 1: Perform Google Custom Search API call with site:simplify.jobs filter
                var searchQuery = $"site:simplify.jobs {request.SearchTerm}";
                Logger.LogInformation($"🌐 Google Custom Search query: {searchQuery}");
                
                var discoveredJobIds = await PerformGoogleCustomSearchAsync(searchQuery, request.MaxResults);

                if (!discoveredJobIds.Any())
                {
                    Logger.LogWarning("No job IDs discovered from Google Custom Search");
                    
                    // If no results, try with broader search terms
                    if (!string.IsNullOrEmpty(request.Location) && request.Location != "Remote")
                    {
                        searchQuery = $"site:simplify.jobs {request.SearchTerm} {request.Location}";
                        Logger.LogInformation($"🔄 Retrying with location: {searchQuery}");
                        discoveredJobIds = await PerformGoogleCustomSearchAsync(searchQuery, request.MaxResults);
                    }
                    
                    if (!discoveredJobIds.Any())
                    {
                        Logger.LogWarning("Still no results - returning empty list");
                        return [];
                    }
                }

                Logger.LogInformation($"✅ Discovered {discoveredJobIds.Count} job IDs from Google Custom Search");

                // Step 2: Use the working SimplifyJobsApiService to fetch detailed job data
                var jobs = await _apiService.FetchJobsByIdsAsync(discoveredJobIds.ToArray(), request.UserId);
                
                Logger.LogInformation($"🎯 Successfully fetched {jobs.Count} jobs using proven API method");
                Logger.LogInformation($"📊 Method: Google Custom Search API + SimplifyJobs Direct API");
                
                return jobs;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Google Custom Search SimplifyJobs discovery");
                return [];
            }
        }

        /// <summary>
        /// Perform Google Custom Search API call
        /// </summary>
        private async Task<List<string>> PerformGoogleCustomSearchAsync(string searchQuery, int maxResults)
        {
            var jobIds = new List<string>();
            
            try
            {
                Logger.LogInformation($"📡 Calling Google Custom Search API with query: {searchQuery}");
                
                // Build the API request URL
                var requestUrl = $"{CUSTOM_SEARCH_API_URL}?" +
                                 $"key={Uri.EscapeDataString(GOOGLE_API_KEY)}&" +
                                 $"cx={Uri.EscapeDataString(SEARCH_ENGINE_ID)}&" +
                                 $"q={Uri.EscapeDataString(searchQuery)}&" +
                                 $"num={Math.Min(maxResults, 10)}"; // Google allows max 10 results per call
                
                Logger.LogInformation($"🌐 API Request URL: {requestUrl.Replace(GOOGLE_API_KEY, "***API_KEY***")}");
                
                // Make the API call
                var response = await _httpClient.GetAsync(requestUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Logger.LogError($"❌ Google Custom Search API error: {response.StatusCode} - {errorContent}");
                    return jobIds;
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                var searchResults = JsonDocument.Parse(jsonContent);
                
                // Extract total results count for logging
                if (searchResults.RootElement.TryGetProperty("searchInformation", out var searchInfo) &&
                    searchInfo.TryGetProperty("totalResults", out var totalResults))
                {
                    Logger.LogInformation($"📊 Total results available: {totalResults.GetString()}");
                }
                
                // Process search result items
                if (searchResults.RootElement.TryGetProperty("items", out var items))
                {
                    var itemsArray = items.EnumerateArray().ToList();
                    Logger.LogInformation($"📄 Processing {itemsArray.Count} search results");
                    
                    foreach (var item in itemsArray)
                    {
                        // Get the link from the result
                        if (item.TryGetProperty("link", out var linkElement))
                        {
                            var url = linkElement.GetString();
                            Logger.LogInformation($"🔗 Found URL: {url}");
                            
                            if (IsSimplifyJobsUrl(url))
                            {
                                var jobId = ExtractJobIdFromUrl(url);
                                if (!string.IsNullOrEmpty(jobId) && !jobIds.Contains(jobId))
                                {
                                    jobIds.Add(jobId);
                                    
                                    // Log title if available for debugging
                                    if (item.TryGetProperty("title", out var titleElement))
                                    {
                                        Logger.LogInformation($"🎯 Extracted job ID: {jobId} - {titleElement.GetString()}");
                                    }
                                    else
                                    {
                                        Logger.LogInformation($"🎯 Extracted job ID: {jobId}");
                                    }
                                }
                            }
                        }
                    }
                    
                    // If we need more results and there are more available, make additional API calls
                    if (jobIds.Count < maxResults && itemsArray.Count == 10)
                    {
                        Logger.LogInformation($"📄 Need more results, making additional API call...");
                        
                        var nextRequestUrl = $"{CUSTOM_SEARCH_API_URL}?" +
                                             $"key={Uri.EscapeDataString(GOOGLE_API_KEY)}&" +
                                             $"cx={Uri.EscapeDataString(SEARCH_ENGINE_ID)}&" +
                                             $"q={Uri.EscapeDataString(searchQuery)}&" +
                                             $"start=11&" +
                                             $"num={Math.Min(maxResults - jobIds.Count, 10)}";
                        
                        var nextResponse = await _httpClient.GetAsync(nextRequestUrl);
                        if (nextResponse.IsSuccessStatusCode)
                        {
                            var nextJsonContent = await nextResponse.Content.ReadAsStringAsync();
                            var nextSearchResults = JsonDocument.Parse(nextJsonContent);
                            
                            if (nextSearchResults.RootElement.TryGetProperty("items", out var nextItems))
                            {
                                foreach (var item in nextItems.EnumerateArray())
                                {
                                    if (item.TryGetProperty("link", out var linkElement))
                                    {
                                        var url = linkElement.GetString();
                                        if (IsSimplifyJobsUrl(url))
                                        {
                                            var jobId = ExtractJobIdFromUrl(url);
                                            if (!string.IsNullOrEmpty(jobId) && !jobIds.Contains(jobId))
                                            {
                                                jobIds.Add(jobId);
                                                Logger.LogInformation($"🎯 Extracted additional job ID: {jobId}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("No items found in Google Custom Search response");
                }
                
                Logger.LogInformation($"✅ Successfully extracted {jobIds.Count} job IDs from Google Custom Search");
                
                return jobIds;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ Error in Google Custom Search API call");
                return jobIds;
            }
        }

        /// <summary>
        /// Extract job ID from SimplifyJobs URL
        /// </summary>
        public static string? ExtractJobIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            var match = JobIdRegex.Match(url);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Validate if URL is a SimplifyJobs job posting
        /// </summary>
        public static bool IsSimplifyJobsUrl(string url)
        {
            return !string.IsNullOrEmpty(url) && 
                   url.Contains("simplify.jobs/p/", StringComparison.OrdinalIgnoreCase);
        }
    }
}