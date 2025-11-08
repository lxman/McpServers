using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumChrome.Core.Models;

namespace SeleniumChrome.Core.Services.Scrapers;

public partial class SimplifyJobsScraper(ILogger<SimplifyJobsScraper> logger) : BaseJobScraper(logger)
{
    public override JobSite SupportedSite => JobSite.SimplifyJobs;

    public override async Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        var jobs = new List<EnhancedJobListing>();
        
        try
        {
            InitializeDriver(config.AntiDetection);
            
            // First ensure we're logged in (critical for Cloudflare bypass)
            bool isAuthenticated = await EnsureAuthentication(config);
            if (!isAuthenticated)
            {
                Logger.LogError("Authentication failed. Cannot proceed with Simplify.jobs scraping.");
                return jobs;
            }
            
            // Build search URL based on your breakthrough analysis
            string searchUrl = BuildSearchUrl(request, config);
            Logger.LogInformation($"Scraping Simplify.jobs: {searchUrl}");
            
            Driver!.Navigate().GoToUrl(searchUrl);
            await Task.Delay(750); // Optimized: Wait for Next.js SSR to complete
            
            // Wait for job listings to load
            await WaitForJobContent();
            
            // 🎯 ENHANCED MULTI-JOB DISCOVERY: Implement proper job card interaction
            Logger.LogInformation("Starting enhanced multi-job discovery...");
            
            var processedJobs = 0;
            var scrollAttempts = 0;
            int maxScrollAttempts = Math.Max(request.MaxResults * 2, 10); // Allow scrolling to find more jobs
            
            while (processedJobs < request.MaxResults && scrollAttempts < maxScrollAttempts)
            {
                // Step 1: Extract any job IDs from current page state
                List<string> currentJobIds = await ExtractJobIds();
                Logger.LogInformation($"📋 Found {currentJobIds.Count} job IDs from page state");
                
                // Step 2: Find visible job cards in the left panel
                List<IWebElement> jobCards = FindJobCardsInLeftPanel();
                Logger.LogInformation($"👀 Found {jobCards.Count} visible job cards");
                
                // Process jobs from current view
                var jobsFoundInThisIteration = 0;
                
                // First, process any job IDs we extracted from page source/URL
                foreach (string jobId in currentJobIds.Take(request.MaxResults - processedJobs))
                {
                    if (await ProcessJobById(jobId, request, jobs))
                    {
                        processedJobs++;
                        jobsFoundInThisIteration++;
                        Logger.LogInformation($"✅ Processed job {processedJobs}/{request.MaxResults} via job ID");
                    }
                    
                    if (processedJobs >= request.MaxResults) break;
                }
                
                // If we still need more jobs and have visible cards, try card interaction
                if (processedJobs < request.MaxResults && jobCards.Count > 0)
                {
                    // Process visible job cards by clicking them
                    int cardsToProcess = Math.Min(jobCards.Count, request.MaxResults - processedJobs);
                    for (var i = 0; i < cardsToProcess; i++)
                    {
                        try
                        {
                            Logger.LogInformation($"🖱️ Clicking job card {i + 1}/{cardsToProcess}");
                            
                            // Click the card to load details
                            await ClickJobCardSafely(jobCards[i]);
                            await Task.Delay(1000); // Wait for right panel to update
                            
                            // Try to get job ID from the updated URL or page state
                            string? jobId = await ExtractJobIdFromCurrentState();
                            
                            if (!string.IsNullOrEmpty(jobId) && await ProcessJobById(jobId, request, jobs))
                            {
                                processedJobs++;
                                jobsFoundInThisIteration++;
                                Logger.LogInformation($"✅ Processed job {processedJobs}/{request.MaxResults} via card click");
                            }
                            else
                            {
                                // Fallback: extract job data directly from current page
                                EnhancedJobListing? directJob = await ExtractJobFromCurrentPage(request);
                                if (directJob != null)
                                {
                                    directJob.SourceSite = SupportedSite;
                                    jobs.Add(directJob);
                                    processedJobs++;
                                    jobsFoundInThisIteration++;
                                    Logger.LogInformation($"✅ Processed job {processedJobs}/{request.MaxResults} via direct extraction");
                                }
                            }
                            
                            await RespectRateLimit(config.RateLimit);
                            
                            if (processedJobs >= request.MaxResults) break;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Error processing job card {i + 1}: {ex.Message}");
                        }
                    }
                }
                
                // If no jobs found in this iteration, try scrolling for more
                if (jobsFoundInThisIteration == 0 && processedJobs < request.MaxResults)
                {
                    Logger.LogInformation("🔄 Scrolling to load more jobs...");
                    await ScrollToLoadMoreJobs();
                    scrollAttempts++;
                    await Task.Delay(2000); // Wait for new jobs to load
                }
                else if (processedJobs >= request.MaxResults)
                {
                    Logger.LogInformation($"🎯 Target reached: {processedJobs} jobs processed");
                    break;
                }
                else
                {
                    // Found some jobs, continue looking for more
                    scrollAttempts = 0; // Reset scroll attempts since we're making progress
                }
                
                // Safety check to prevent infinite loops
                if (scrollAttempts >= maxScrollAttempts)
                {
                    Logger.LogInformation($"⚠️ Max scroll attempts reached. Processed {processedJobs} jobs.");
                    break;
                }
            }
            
            Logger.LogInformation($"Successfully scraped {jobs.Count} jobs from Simplify.jobs");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error scraping Simplify.jobs: {ex.Message}");
        }
        
        return jobs;
    }

    
    private async Task<bool> ProcessJobById(string jobId, EnhancedScrapeRequest request, List<EnhancedJobListing> jobs)
    {
        try
        {
            Logger.LogInformation($"🔍 Processing job ID: {jobId}");
            
            // Fetch job details via API
            JsonElement? jobDetails = await FetchJobDetailsViaApi(jobId);
            if (jobDetails.HasValue)
            {
                EnhancedJobListing? enhancedJob = ConvertToEnhancedJobListing(jobDetails.Value, request);
                if (enhancedJob != null)
                {
                    enhancedJob.SourceSite = SupportedSite;
                    jobs.Add(enhancedJob);
                    Logger.LogInformation($"✅ Successfully processed: {enhancedJob.Title} at {enhancedJob.Company}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error processing job ID {jobId}: {ex.Message}");
        }
        
        return false;
    }
    
    private async Task<string?> ExtractJobIdFromCurrentState()
    {
        try
        {
            // Check URL for jobId parameter
            string currentUrl = Driver!.Url;
            Match urlMatch = Regex.Match(currentUrl, @"jobId=([a-f0-9-]{8,50})", RegexOptions.IgnoreCase);
            if (urlMatch.Success)
            {
                return urlMatch.Groups[1].Value;
            }
            
            // Try JavaScript to get current selected job ID
            object? jsResult = ((IJavaScriptExecutor)Driver!).ExecuteScript(@"
                try {
                    // Check URL params
                    const urlParams = new URLSearchParams(window.location.search);
                    const jobId = urlParams.get('jobId');
                    if (jobId) return jobId;
                    
                    // Check for selected job in React state or DOM
                    const selectedElements = document.querySelectorAll('[class*=""selected""], [class*=""active""]');
                    for (let el of selectedElements) {
                        const id = el.getAttribute('data-job-id') || el.getAttribute('data-id');
                        if (id && id.match(/^[a-f0-9-]{8,50}$/i)) return id;
                    }
                    
                    return null;
                } catch (e) {
                    return null;
                }
            ");
            
            if (jsResult?.ToString()?.Length > 10)
            {
                return jsResult.ToString();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting job ID from current state: {ex.Message}");
        }
        
        return null;
    }
    
    private async Task<EnhancedJobListing?> ExtractJobFromCurrentPage(EnhancedScrapeRequest request)
    {
        try
        {
            Logger.LogInformation("Extracting job data directly from current page...");
            
            // Extract basic job info from the right panel
            string? title = null;
            string? company = null;
            string? location = null;
            string? url = null;
            
            // Try multiple selectors for job title
            var titleSelectors = new[]
            {
                "h1", "h2", "[data-testid*='title']", ".job-title", "[class*='title']"
            };
            
            foreach (string selector in titleSelectors)
            {
                try
                {
                    IWebElement element = Driver!.FindElement(By.CssSelector(selector));
                    title = element.Text?.Trim();
                    if (!string.IsNullOrEmpty(title))
                    {
                        Logger.LogInformation($"Found job title: {title}");
                        break;
                    }
                }
                catch { }
            }
            
            // Try multiple selectors for company name
            var companySelectors = new[]
            {
                "[data-testid*='company']", ".company-name", "[class*='company']", "h3", "h4"
            };
            
            foreach (string selector in companySelectors)
            {
                try
                {
                    IWebElement element = Driver!.FindElement(By.CssSelector(selector));
                    company = element.Text?.Trim();
                    if (!string.IsNullOrEmpty(company) && company != title)
                    {
                        Logger.LogInformation($"Found company name: {company}");
                        break;
                    }
                }
                catch { }
            }
            
            // Extract location
            try
            {
                IWebElement locationElement = Driver!.FindElement(By.CssSelector("[data-testid*='location'], .location, [class*='location']"));
                location = locationElement.Text?.Trim();
            }
            catch { }
            
            // Try to find SimplifyJobs URL for this job
            url = $"https://simplify.jobs/jobs/{Guid.NewGuid()}"; // Fallback URL
            
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(company))
            {
                var job = new EnhancedJobListing
                {
                    Title = title,
                    Company = company,
                    Location = location ?? "Location not specified",
                    Url = url,
                    Summary = "Job details extracted from page",
                    ScrapedAt = DateTime.UtcNow,
                    SourceSite = SupportedSite,
                    Benefits = "📄 Basic details extracted from page"
                };
                
                Logger.LogInformation("Successfully extracted job data from page");
                return job;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting job from current page: {ex.Message}");
        }
        
        return null;
    }
    
    private async Task ScrollToLoadMoreJobs()
    {
        try
        {
            var js = (IJavaScriptExecutor)Driver!;
            
            // Try different scrolling strategies
            js.ExecuteScript(@"
                // Strategy 1: Scroll the job list container
                const jobContainers = document.querySelectorAll('[class*=""job""][class*=""list""], [class*=""left""], [class*=""sidebar""]');
                let scrolled = false;
                
                for (let container of jobContainers) {
                    const style = window.getComputedStyle(container);
                    if (style.overflowY === 'scroll' || style.overflowY === 'auto') {
                        container.scrollTop += 500;
                        scrolled = true;
                        break;
                    }
                }
                
                // Strategy 2: Scroll the main window
                if (!scrolled) {
                    window.scrollBy(0, 500);
                }
                
                // Strategy 3: Trigger any infinite scroll mechanisms
                window.dispatchEvent(new Event('scroll'));
                document.dispatchEvent(new Event('scroll'));
            ");
            
            Logger.LogInformation("📜 Scrolled to trigger loading of more jobs");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error scrolling: {ex.Message}");
        }
    }
    
    private async Task<bool> EnsureAuthentication(SiteConfiguration config)
    {
        try
        {
            // Navigate to main page first
            Driver!.Navigate().GoToUrl("https://simplify.jobs");
            await Task.Delay(750); // Optimized from 1500
            
            // Quick check for authentication - don't spend too much time here
            Logger.LogInformation("Performing quick authentication check...");
            
            // Simple check: if we can access /jobs without being redirected to login
            try
            {
                Driver.Navigate().GoToUrl("https://simplify.jobs/jobs");
                await Task.Delay(250); // Optimized: Quick check
                
                string currentUrl = Driver.Url.ToLower();
                bool isAuthenticated = currentUrl.Contains("/jobs") && 
                                       !currentUrl.Contains("login") && 
                                       !currentUrl.Contains("signin");
                
                if (isAuthenticated)
                {
                    Logger.LogInformation("Authentication confirmed - proceeding with scraping");
                    return true;
                }

                Logger.LogWarning($"Authentication unclear. Current URL: {currentUrl}");
                Logger.LogInformation("Proceeding with scraping attempt anyway");
                return true; // Proceed anyway
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Authentication check failed: {ex.Message}");
                Logger.LogInformation("Proceeding with scraping attempt");
                return true; // Proceed anyway
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error checking authentication: {ex.Message}");
            Logger.LogInformation("Proceeding with scraping attempt despite authentication check failure");
            return true; // Proceed anyway
        }
    }

    private string BuildSearchUrl(EnhancedScrapeRequest request, SiteConfiguration config)
    {
        // Based on your breakthrough analysis: URL-based search system
        var searchTerms = new List<string>();
        
        // Add .NET specific terms
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            searchTerms.Add(request.SearchTerm);
        }
        else
        {
            // Default .NET searches based on your profile
            searchTerms.AddRange([".NET", "C#", "backend", "full stack"]);
        }
        
        var queryParams = new List<string>();
        
        // Search query
        if (searchTerms.Count > 0)
        {
            queryParams.Add($"query={Uri.EscapeDataString(string.Join(" ", searchTerms))}");
        }
        
        // Location filter - prioritize remote
        string location = !string.IsNullOrEmpty(request.Location) ? request.Location : "Remote in USA";
        queryParams.Add($"state={Uri.EscapeDataString(location)}");
        
        // Experience level - target senior roles for your profile
        queryParams.Add("experience=Senior%3BExpert%20or%20higher");
        
        // Category filters for relevant tech roles
        var categories = new[]
        {
            "Backend Engineering",
            "Full Stack Development", 
            "Software Engineering",
            "Platform Engineering",
            "DevOps & Infrastructure"
        };
        queryParams.Add($"category={Uri.EscapeDataString(string.Join(";", categories))}");
        
        return $"{config.BaseUrl}/jobs?{string.Join("&", queryParams)}";
    }

    private async Task WaitForJobContent()
    {
        try
        {
            // Optimized timeout for faster loading
            var wait = new WebDriverWait(Driver!, TimeSpan.FromSeconds(20));
            
            Logger.LogInformation("Waiting for job content to load...");
            
            // Wait for job listings to appear (Next.js SSR content)
            // Use a more flexible approach with multiple possible indicators
            bool jobContentLoaded = wait.Until(d => {
                try
                {
                    // Check for any of these indicators that the page has content
                    var indicators = new[]
                    {
                        "[data-testid*='job']",
                        ".job-listing", 
                        "[class*='job-card']",
                        "[class*='job-item']",
                        "a[href*='/jobs/']",
                        // Also check for the job count text
                        "[class*='showing']", // "Showing X of Y Jobs"
                        "[class*='results']",
                        // Or any substantial page content
                        "main",
                        ".content",
                        "#content"
                    };
                    
                    foreach (string selector in indicators)
                    {
                        ReadOnlyCollection<IWebElement> elements = d.FindElements(By.CssSelector(selector));
                        if (elements.Count > 0)
                        {
                            Logger.LogInformation($"Job content detected with selector: {selector} ({elements.Count} elements)");
                            return true;
                        }
                    }
                    
                    return false;
                }
                catch
                {
                    return false;
                }
            });
            
            if (jobContentLoaded)
            {
                Logger.LogInformation("Job content successfully loaded");
                // Optimized wait for any dynamic content
                await Task.Delay(500);
            }
        }
        catch (WebDriverTimeoutException)
        {
            Logger.LogWarning("Timeout waiting for job content to load - proceeding anyway");
            // Don't fail, just proceed with whatever content is available
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error waiting for job content: {ex.Message} - proceeding anyway");
        }
    }

    private async Task<List<string>> ExtractJobIds()
    {
        var jobIds = new List<string>();
        
        try
        {
            Logger.LogInformation("Starting enhanced job ID extraction...");
            
            // First, let's see what's on the page
            string pageTitle = Driver!.Title;
            string currentUrl = Driver.Url;
            Logger.LogInformation($"Current page: {pageTitle} at {currentUrl}");
            
            // Strategy 1: Try to extract job IDs from page source (for JavaScript-rendered content)
            jobIds.AddRange(await ExtractJobIdsFromPageSource());
            
            // Strategy 2: Extract job IDs using JavaScript execution (most reliable for Next.js apps)
            if (jobIds.Count == 0)
            {
                jobIds.AddRange(await ExtractJobIdsWithJavaScript());
            }
            
            // Strategy 3: Traditional DOM selectors (fallback)
            if (jobIds.Count == 0)
            {
                jobIds.AddRange(await ExtractJobIdsFromDOM());
            }
            
            // Strategy 4: Interactive clicking to reveal job URLs (DISABLED - causes navigation overhead)
            // if (jobIds.Count == 0)
            // {
            //     jobIds.AddRange(await ExtractJobIdsByInteraction());
            // }
            
            List<string> uniqueJobIds = jobIds.Distinct().Take(10).ToList(); // Limit to 10 for testing
            Logger.LogInformation($"Successfully extracted {uniqueJobIds.Count} unique job IDs");
            
            if (uniqueJobIds.Count == 0)
            {
                Logger.LogWarning("No job IDs found with any extraction method");
                await SaveDebugScreenshot();
            }
            else
            {
                Logger.LogInformation($"Extracted job IDs: {string.Join(", ", uniqueJobIds.Take(5))}");
            }
            
            return uniqueJobIds;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error extracting job IDs: {ex.Message}");
            return [];
        }
    }

    private async Task<List<string>> ExtractJobIdsFromPageSource()
    {
        var jobIds = new List<string>();
        
        try
        {
            Logger.LogInformation("Attempting to extract job IDs from page source...");
            
            // First, extract job ID from current URL (as seen in the screenshot)
            string currentUrl = Driver!.Url;
            Match urlJobIdMatch = Regex.Match(currentUrl, @"jobId=([a-f0-9-]{8,50})", RegexOptions.IgnoreCase);
            if (urlJobIdMatch.Success)
            {
                string urlJobId = urlJobIdMatch.Groups[1].Value;
                if (IsValidJobId(urlJobId))
                {
                    jobIds.Add(urlJobId);
                    Logger.LogInformation($"Extracted job ID from URL: {urlJobId}");
                }
            }
            
            string pageSource = Driver.PageSource;
            
            // Enhanced patterns specifically for Simplify.Jobs based on screenshot analysis
            var patterns = new[]
            {
                @"jobId=([a-f0-9-]{8,50})", // Query parameters (highest priority)
                @"""id""\s*:\s*""([a-f0-9-]{8,50})""", // JSON: "id": "uuid"
                @"""jobId""\s*:\s*""([a-f0-9-]{8,50})""", // JSON jobId field
                @"job[_-]?id[""']\s*:\s*[""']([a-f0-9-]{8,50})[""']", // jobId: "uuid"
                @"data-job-id=[""']([a-f0-9-]{8,50})[""']", // HTML data attributes
                @"/jobs/([a-f0-9-]{8,50})", // URL patterns
                @"simplify\.jobs/jobs/([a-f0-9-]{8,50})", // Full URLs
                @"""([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})""", // Pure UUID patterns
            };
            
            foreach (string pattern in patterns)
            {
                MatchCollection matches = Regex.Matches(pageSource, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        string jobId = match.Groups[1].Value;
                        if (IsValidJobId(jobId) && !jobIds.Contains(jobId))
                        {
                            jobIds.Add(jobId);
                            Logger.LogDebug($"Found job ID from page source: {jobId}");
                        }
                    }
                }
                
                if (jobIds.Count > 3) // Limit to avoid too many IDs
                {
                    Logger.LogInformation($"Extracted {jobIds.Count} job IDs using pattern: {pattern}");
                    break;
                }
            }
            
            if (jobIds.Count > 0)
            {
                Logger.LogInformation($"Total job IDs extracted from page source: {jobIds.Count}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting job IDs from page source: {ex.Message}");
        }
        
        return jobIds;
    }

    private async Task<List<string>> ExtractJobIdsWithJavaScript()
    {
        var jobIds = new List<string>();
        
        try
        {
            Logger.LogInformation("Attempting to extract job IDs using JavaScript execution...");
            
            // Enhanced JavaScript to find job data in Simplify.Jobs specifically
            var jsScript = @"
                var jobIds = [];
                
                // Method 1: Check current URL for jobId parameter (most reliable)
                try {
                    var urlParams = new URLSearchParams(window.location.search);
                    var currentJobId = urlParams.get('jobId');
                    if (currentJobId) {
                        jobIds.push(currentJobId);
                    }
                } catch (e) {}
                
                // Method 2: Look for Next.js page props or app state
                try {
                    if (window.__NEXT_DATA__ && window.__NEXT_DATA__.props) {
                        var pageProps = window.__NEXT_DATA__.props.pageProps;
                        if (pageProps.jobs) {
                            pageProps.jobs.forEach(job => {
                                if (job.id) jobIds.push(job.id);
                            });
                        }
                        if (pageProps.jobListings) {
                            pageProps.jobListings.forEach(job => {
                                if (job.id) jobIds.push(job.id);
                            });
                        }
                        if (pageProps.initialJobs) {
                            pageProps.initialJobs.forEach(job => {
                                if (job.id) jobIds.push(job.id);
                            });
                        }
                        // Check for any job-related data in pageProps
                        Object.keys(pageProps).forEach(key => {
                            if (key.toLowerCase().includes('job') && Array.isArray(pageProps[key])) {
                                pageProps[key].forEach(item => {
                                    if (item && item.id && typeof item.id === 'string' && item.id.match(/^[a-f0-9-]{8,50}$/i)) {
                                        jobIds.push(item.id);
                                    }
                                });
                            }
                        });
                    }
                } catch (e) {}
                
                // Method 3: Check for React app state or Redux store
                try {
                    if (window.__REDUX_STORE__) {
                        var state = window.__REDUX_STORE__.getState();
                        if (state.jobs) {
                            Object.values(state.jobs).forEach(job => {
                                if (job.id) jobIds.push(job.id);
                            });
                        }
                    }
                } catch (e) {}
                
                // Method 4: Look for job data in global variables
                try {
                    if (window.jobData) {
                        window.jobData.forEach(job => {
                            if (job.id) jobIds.push(job.id);
                        });
                    }
                    if (window.jobs) {
                        window.jobs.forEach(job => {
                            if (job.id) jobIds.push(job.id);
                        });
                    }
                    if (window.initialData && window.initialData.jobs) {
                        window.initialData.jobs.forEach(job => {
                            if (job.id) jobIds.push(job.id);
                        });
                    }
                } catch (e) {}
                
                // Method 5: Extract from script tags containing JSON data
                try {
                    var scripts = document.querySelectorAll('script');
                    scripts.forEach(script => {
                        if (script.textContent && script.textContent.includes('job')) {
                            var matches = script.textContent.match(/""id""\s*:\s*""([a-f0-9-]{8,50})""/gi);
                            if (matches) {
                                matches.forEach(match => {
                                    var id = match.match(/""([a-f0-9-]{8,50})""/i);
                                    if (id && id[1]) jobIds.push(id[1]);
                                });
                            }
                        }
                    });
                } catch (e) {}
                
                // Method 6: Look for data attributes on job elements
                try {
                    var jobElements = document.querySelectorAll('[data-testid*=""job""], .job-card, [class*=""job""]');
                    jobElements.forEach(el => {
                        var id = el.getAttribute('data-job-id') || el.getAttribute('data-id');
                        if (id && id.match(/^[a-f0-9-]{8,50}$/i)) {
                            jobIds.push(id);
                        }
                    });
                } catch (e) {}
                
                return [...new Set(jobIds)]; // Return unique job IDs
            ";
            
            object? result = ((IJavaScriptExecutor)Driver!).ExecuteScript(jsScript);
            
            if (result is ReadOnlyCollection<object> resultArray)
            {
                foreach (object item in resultArray)
                {
                    if (item is string jobId && IsValidJobId(jobId))
                    {
                        jobIds.Add(jobId);
                    }
                }
                
                if (jobIds.Count > 0)
                {
                    Logger.LogInformation($"JavaScript extraction found {jobIds.Count} job IDs");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error extracting job IDs with JavaScript: {ex.Message}");
        }
        
        return jobIds;
    }

    private async Task<List<string>> ExtractJobIdsFromDOM()
    {
        var jobIds = new List<string>();
        
        try
        {
            Logger.LogInformation("Attempting traditional DOM extraction...");
            
            // More comprehensive selectors based on common patterns
            var jobLinkSelectors = new[]
            {
                "h3.text-left",                    // PRIORITY 1: Actual job title elements 
                "button.inline-flex",              // PRIORITY 2: Job card containers
                "button > div.mx-auto",            // PRIORITY 3: Job card parent containers
                "a[href*='/jobs/']",
                "a[href*='/job/']", 
                "[data-job-id]",
                ".job-card a",
                ".job-listing a",
                "[class*='job-card'] a",
                "[class*='job-item'] a",
                "[data-testid*='job'] a",
                "a[href*='job']",
                "[onClick*='job']"
            };
            
            foreach (string selector in jobLinkSelectors)
            {
                try
                {
                    ReadOnlyCollection<IWebElement> elements = Driver!.FindElements(By.CssSelector(selector));
                    Logger.LogDebug($"Selector '{selector}' found {elements.Count} elements");
                    
                    foreach (IWebElement element in elements)
                    {
                        try
                        {
                            // Try multiple attributes
                            var attributes = new[] { "href", "data-job-id", "onclick", "data-href" };
                            
                            foreach (string attr in attributes)
                            {
                                string? value = element.GetAttribute(attr);
                                if (!string.IsNullOrEmpty(value))
                                {
                                    MatchCollection matches = Regex.Matches(value, @"([a-f0-9-]{8,50})", RegexOptions.IgnoreCase);
                                    foreach (Match match in matches)
                                    {
                                        string jobId = match.Groups[1].Value;
                                        if (IsValidJobId(jobId) && !jobIds.Contains(jobId))
                                        {
                                            jobIds.Add(jobId);
                                            Logger.LogDebug($"Extracted job ID from {attr}: {jobId}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogDebug($"Error processing element: {ex.Message}");
                        }
                    }
                    
                    if (jobIds.Count > 0) break; // Stop at first successful selector
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"Selector {selector} failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error in DOM extraction: {ex.Message}");
        }
        
        return jobIds;
    }

    private async Task<List<string>> ExtractJobIdsByInteraction()
    {
        var jobIds = new List<string>();
        
        try
        {
            Logger.LogInformation("Attempting job ID extraction by interaction...");
            
            // Find clickable job elements
            ReadOnlyCollection<IWebElement> jobElements = Driver!.FindElements(By.CssSelector(".job-card, .job-listing, [class*='job-item'], [data-testid*='job']"));
            
            Logger.LogInformation($"Found {jobElements.Count} potentially clickable job elements");
            
            for (var i = 0; i < Math.Min(jobElements.Count, 3); i++) // Test first 3 elements
            {
                try
                {
                    IWebElement element = jobElements[i];
                    string originalUrl = Driver.Url;
                    
                    // Try clicking the element
                    element.Click();
                    await Task.Delay(500); // Optimized: Wait for navigation or URL change
                    
                    string newUrl = Driver.Url;
                    if (newUrl != originalUrl)
                    {
                        // URL changed, extract job ID from new URL
                        Match match = Regex.Match(newUrl, @"/jobs/([a-f0-9-]{8,50})", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string jobId = match.Groups[1].Value;
                            if (IsValidJobId(jobId) && !jobIds.Contains(jobId))
                            {
                                jobIds.Add(jobId);
                                Logger.LogInformation($"Extracted job ID via interaction: {jobId}");
                            }
                        }
                        
                        // Navigate back to job list
                        Driver.Navigate().Back();
                        await Task.Delay(1000); // Optimized navigation back wait
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"Interaction with element {i} failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error in interaction-based extraction: {ex.Message}");
        }
        
        return jobIds;
    }

    private async Task SaveDebugScreenshot()
    {
        try
        {
            Screenshot screenshot = ((ITakesScreenshot)Driver!).GetScreenshot();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var screenshotPath = $"C:\\Users\\jorda\\RiderProjects\\SeleniumChromeTool\\Screenshots\\simplify_debug_{timestamp}.png";
            screenshot.SaveAsFile(screenshotPath);
            Logger.LogInformation($"Debug screenshot saved: {screenshotPath}");
            
            // Also log page source length for debugging
            int pageSourceLength = Driver.PageSource.Length;
            Logger.LogInformation($"Page source length: {pageSourceLength} characters");
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Could not save debug screenshot: {ex.Message}");
        }
    }

    private async Task<JsonElement?> FetchJobDetailsViaApi(string jobId)
    {
        try
        {
            Logger.LogInformation($"🎯 Fetching job details and EXTERNAL URL for job ID: {jobId}");
            
            // Handle mock job IDs (temporary testing solution)
            if (jobId.StartsWith("test-job-"))
            {
                Logger.LogInformation($"Processing mock job ID for testing: {jobId}");
                
                // Return a mock job structure for testing
                var mockJobData = $@"{{
                    ""title"": ""Senior .NET Developer"",
                    ""id"": ""{jobId}"",
                    ""description"": ""We are seeking an experienced .NET developer with 5+ years of experience in building scalable web applications. Must have expertise in C#, ASP.NET Core, and cloud platforms."",
                    ""company"": {{
                        ""name"": ""Simplify Test Company"",
                        ""id"": ""test-company-id"",
                        ""description"": ""A leading technology company focused on innovative solutions"",
                        ""size"": ""100-500 employees"",
                        ""funding_stage"": ""Series B""
                    }},
                    ""locations"": [{{
                        ""value"": ""Remote in USA"",
                        ""country"": ""United States""
                    }}],
                    ""skills"": [{{
                        ""name"": "".NET""
                    }}, {{
                        ""name"": ""C#""
                    }}, {{
                        ""name"": ""ASP.NET Core""
                    }}, {{
                        ""name"": ""Azure""
                    }}],
                    ""requirements"": [
                        ""5+ years of .NET development experience"",
                        ""Strong knowledge of C# and ASP.NET Core"",
                        ""Experience with cloud platforms (Azure/AWS)""
                    ],
                    ""url"": ""https://job-boards.greenhouse.io/example-company/jobs/12345""
                }}";
                
                JsonDocument jsonDoc = JsonDocument.Parse(mockJobData);
                return jsonDoc.RootElement;
            }
            
            // 🚀 ENHANCED: Call the SimplifyJobs API to get the REAL external URL
            var apiEndpoints = new[]
            {
                $"https://api.simplify.jobs/v2/job-posting/:id/{jobId}/company",
                $"https://api.simplify.jobs/v2/job-posting/id/{jobId}/company",
                $"https://api.simplify.jobs/v2/job-posting/{jobId}/company", 
                $"https://api.simplify.jobs/v2/job/{jobId}",
                $"https://api.simplify.jobs/v2/jobs/{jobId}"
            };
            
            foreach (string apiUrl in apiEndpoints)
            {
                try
                {
                    Logger.LogInformation($"🔍 Trying API endpoint: {apiUrl}");
                    
                    // 🔧 Enhanced JavaScript with better error handling and external URL extraction
                    var jsScript = $@"
                        var callback = arguments[arguments.length - 1];
                        
                        fetch('{apiUrl}', {{
                            method: 'GET',
                            credentials: 'include',
                            headers: {{
                                'Accept': 'application/json',
                                'Content-Type': 'application/json',
                                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
                                'Referer': 'https://simplify.jobs/jobs'
                            }}
                        }})
                        .then(response => {{
                            console.log('🔍 API Response status:', response.status, response.statusText);
                            if (response.ok) {{
                                return response.json();
                            }} else {{
                                throw new Error('HTTP ' + response.status + ': ' + response.statusText);
                            }}
                        }})
                        .then(data => {{
                            console.log('✅ API call successful for job {jobId}');
                            console.log('📋 Job data received:', data.title || 'No title');
                            console.log('🏢 Company:', data.company?.name || 'No company');
                            console.log('🎯 External URL:', data.url || 'NO EXTERNAL URL');
                            
                            // 🚀 BREAKTHROUGH: Extract the real external URL
                            if (data.url && data.url !== '' && !data.url.includes('simplify.jobs')) {{
                                console.log('🎉 REAL EXTERNAL URL FOUND:', data.url);
                            }} else if (data.url && data.url.includes('simplify.jobs')) {{
                                console.log('⚠️ SimplifyJobs URL (not external):', data.url);
                            }} else {{
                                console.log('❌ No external URL in response');
                            }}
                            
                            callback(JSON.stringify(data));
                        }})
                        .catch(error => {{
                            console.log('❌ API Error:', error.message);
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
                            JsonElement jobElement = jsonDoc.RootElement;
                            
                            // 🎯 CRITICAL: Check if we got the external URL
                            if (jobElement.TryGetProperty("url", out JsonElement urlElement))
                            {
                                string? externalUrl = urlElement.GetString();
                                if (!string.IsNullOrEmpty(externalUrl))
                                {
                                    if (!externalUrl.Contains("simplify.jobs"))
                                    {
                                        Logger.LogInformation($"🎉 SUCCESS! Found REAL external URL: {externalUrl}");
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"⚠️ URL is still SimplifyJobs URL: {externalUrl}");
                                    }
                                }
                                else
                                {
                                    Logger.LogWarning($"❌ External URL field is empty for job {jobId}");
                                }
                            }
                            else
                            {
                                Logger.LogWarning($"❌ No 'url' field in API response for job {jobId}");
                            }
                            
                            Logger.LogInformation($"✅ Successfully fetched job details from: {apiUrl}");
                            return jobElement;
                        }
                        catch (JsonException ex)
                        {
                            Logger.LogWarning($"Invalid JSON response from {apiUrl}: {ex.Message}. Response: {jsonString}");
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"Empty or null response from {apiUrl}. Result: {result}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"API endpoint {apiUrl} failed: {ex.Message}");
                }
            }
            
            // Method 2: Try to extract job data directly from the page if API fails
            Logger.LogInformation($"⚠️ All API calls failed for job {jobId}, attempting to extract data from current page");
            return await ExtractJobDataFromPage(jobId);
            
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error fetching job details for ID {jobId}: {ex.Message}");
        }
        
        return null;
    }

    private async Task<JsonElement?> ExtractJobDataFromPage(string jobId)
    {
        try
        {
            Logger.LogInformation("Extracting job data directly from current page for job ID: {jobId}", jobId);
            
            // Extract job details using the actual page structure discovered via browser inspection
            var jobData = new Dictionary<string, object>();
            
            try
            {
                // Extract title using the actual selector from accessibility audit
                var titleSelectors = new[] { 
                    "h3.text-left", 
                    "h3[class*='text-left']",
                    "h3",
                    "[data-testid*='title']", 
                    ".job-title", 
                    ".title",
                    "h1", 
                    "h2"
                };
                
                foreach (string selector in titleSelectors)
                {
                    try
                    {
                        IWebElement titleElement = Driver.FindElement(By.CssSelector(selector));
                        if (!string.IsNullOrEmpty(titleElement.Text))
                        {
                            jobData["title"] = titleElement.Text.Trim();
                            Logger.LogInformation("Found job title: {title}", titleElement.Text.Trim());
                            break;
                        }
                    }
                    catch { }
                }
                
                // Extract company name - look for common patterns
                var companySelectors = new[] { 
                    "h4", 
                    "h2", 
                    "[data-testid*='company']", 
                    ".company-name", 
                    ".company",
                    "p[class*='company']",
                    "div[class*='company']",
                    "span[class*='company']"
                };
                
                foreach (string selector in companySelectors)
                {
                    try
                    {
                        IWebElement companyElement = Driver.FindElement(By.CssSelector(selector));
                        if (!string.IsNullOrEmpty(companyElement.Text) && 
                            !companyElement.Text.Contains("Desktop Software Engineer") && // Don't confuse title with company
                            companyElement.Text.Length < 100) // Company names shouldn't be too long
                        {
                            jobData["company"] = new Dictionary<string, object>
                            {
                                ["name"] = companyElement.Text.Trim()
                            };
                            Logger.LogInformation("Found company name: {company}", companyElement.Text.Trim());
                            break;
                        }
                    }
                    catch { }
                }
                
                // Extract description from various possible locations
                var descSelectors = new[] { 
                    "[data-testid*='description']", 
                    ".job-description", 
                    ".description", 
                    ".content",
                    "div[class*='description']",
                    "section[class*='description']",
                    "main p", 
                    "div p",
                    "section p"
                };
                
                foreach (string selector in descSelectors)
                {
                    try
                    {
                        IWebElement descElement = Driver.FindElement(By.CssSelector(selector));
                        if (!string.IsNullOrEmpty(descElement.Text) && descElement.Text.Length > 50)
                        {
                            jobData["description"] = descElement.Text.Trim();
                            Logger.LogInformation("Found job description: {desc}", descElement.Text.Substring(0, Math.Min(100, descElement.Text.Length)) + "...");
                            break;
                        }
                    }
                    catch { }
                }
                
                // Extract location information
                var locationSelectors = new[] {
                    "[data-testid*='location']",
                    ".location",
                    "span[class*='location']",
                    "div[class*='location']",
                    "p[class*='location']"
                };
                
                foreach (string selector in locationSelectors)
                {
                    try
                    {
                        IWebElement locationElement = Driver.FindElement(By.CssSelector(selector));
                        if (!string.IsNullOrEmpty(locationElement.Text))
                        {
                            jobData["locations"] = new[] { 
                                new { 
                                    value = locationElement.Text.Trim(), 
                                    country = "United States" 
                                } 
                            };
                            Logger.LogInformation("Found location: {location}", locationElement.Text.Trim());
                            break;
                        }
                    }
                    catch { }
                }
                
                // If no specific location found, default to Remote based on search criteria
                if (!jobData.ContainsKey("locations"))
                {
                    jobData["locations"] = new[] { new { value = "Remote in USA", country = "United States" } };
                }
                
                // Extract skills/technologies if available
                var skillsSelectors = new[] {
                    "[data-testid*='skill']",
                    ".skill",
                    ".technology",
                    "span[class*='skill']",
                    "div[class*='skill']"
                };
                
                var skills = new List<object>();
                foreach (string selector in skillsSelectors)
                {
                    try
                    {
                        ReadOnlyCollection<IWebElement> skillElements = Driver.FindElements(By.CssSelector(selector));
                        foreach (IWebElement skillElement in skillElements.Take(10)) // Limit to 10 skills
                        {
                            if (!string.IsNullOrEmpty(skillElement.Text) && skillElement.Text.Length < 50)
                            {
                                skills.Add(new { name = skillElement.Text.Trim() });
                            }
                        }
                        if (skills.Count > 0) break;
                    }
                    catch { }
                }
                
                if (skills.Count > 0)
                {
                    jobData["skills"] = skills;
                    Logger.LogInformation("Found {count} skills", skills.Count);
                }
                
                // Add the job ID and URL
                jobData["id"] = jobId;
                jobData["url"] = Driver.Url;
                
                // Only return data if we found at least a title
                if (jobData.ContainsKey("title"))
                {
                    // Convert to JSON
                    string jsonString = JsonSerializer.Serialize(jobData);
                    JsonDocument jsonDoc = JsonDocument.Parse(jsonString);
                    
                    Logger.LogInformation("Successfully extracted job data from page");
                    return jsonDoc.RootElement;
                }

                Logger.LogWarning("Could not find job title on page - skipping extraction");

            }
            catch (Exception ex)
            {
                Logger.LogWarning("Error extracting job data from page: {error}", ex.Message);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Error in page data extraction: {error}", ex.Message);
        }
        
        return null;
    }

    private EnhancedJobListing? ConvertToEnhancedJobListing(JsonElement jobData, EnhancedScrapeRequest request)
    {
        try
        {
            // 🔍 DEBUG: Log the actual JSON structure we received
            Logger.LogInformation($"🔍 DEBUG: Converting job data with properties: {string.Join(", ", jobData.EnumerateObject().Select(p => p.Name))}");
            
            // Extract data based on your breakthrough analysis structure
            if (!jobData.TryGetProperty("title", out JsonElement titleElement))
            {
                Logger.LogWarning($"❌ Missing title property in JSON. Available properties: {string.Join(", ", jobData.EnumerateObject().Select(p => p.Name))}");
                return null;
            }

            // 🎯 FIX: Company information is in the nested 'job' object, not at root level
            JsonElement companyElement = default;
            if (!jobData.TryGetProperty("job", out JsonElement jobElement) || 
                !jobElement.TryGetProperty("company", out companyElement))
            {
                Logger.LogWarning($"❌ Missing job.company property in JSON. Available properties: {string.Join(", ", jobData.EnumerateObject().Select(p => p.Name))}");
                return null;
            }
            
            var job = new EnhancedJobListing
            {
                Title = titleElement.GetString() ?? "Unknown Title",
                Company = companyElement.TryGetProperty("name", out JsonElement compNameElement) 
                    ? compNameElement.GetString() ?? "Unknown Company"
                    : "Unknown Company",
                SourceSite = SupportedSite,
                ScrapedAt = DateTime.UtcNow
            };
            
            // 🎯 CRITICAL ENHANCEMENT: Extract the REAL external URL
            string? externalUrl = null;
            string? simplifyJobsUrl = null;
            
            if (jobData.TryGetProperty("url", out JsonElement urlElement))
            {
                string? url = urlElement.GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    if (!url.Contains("simplify.jobs"))
                    {
                        // 🎉 This is the real external URL (Greenhouse, Workday, etc.)
                        externalUrl = url;
                        Logger.LogInformation($"✅ REAL external URL found: {externalUrl}");
                    }
                    else
                    {
                        // This is a SimplifyJobs URL, store separately
                        simplifyJobsUrl = url;
                        Logger.LogInformation($"📋 SimplifyJobs URL: {simplifyJobsUrl}");
                    }
                }
            }
            
            // 🚀 BREAKTHROUGH: Set the main URL field to the EXTERNAL URL
            if (!string.IsNullOrEmpty(externalUrl))
            {
                job.Url = externalUrl; // The Apply button destination!
                Logger.LogInformation($"🎯 Job URL set to EXTERNAL URL: {externalUrl}");
            }
            else if (jobData.TryGetProperty("id", out JsonElement idElement))
            {
                // Fallback: Use SimplifyJobs URL if no external URL found
                string? jobId = idElement.GetString();
                job.Url = $"https://simplify.jobs/jobs/{jobId}";
                Logger.LogWarning($"⚠️ No external URL found, using SimplifyJobs URL: {job.Url}");
            }
            
            // Extract description
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
            
            // Extract company details - store in Notes for now since model doesn't have separate company fields
            var companyInfo = new List<string>();
            if (companyElement.TryGetProperty("description", out JsonElement compDescElement))
            {
                companyInfo.Add($"Company: {compDescElement.GetString()}");
            }
            
            if (companyElement.TryGetProperty("size", out JsonElement sizeElement))
            {
                companyInfo.Add($"Size: {sizeElement.GetString()}");
            }
            
            // 🎯 Add URL information to Notes for transparency
            if (!string.IsNullOrEmpty(externalUrl))
            {
                companyInfo.Add($"External Apply URL: {externalUrl}");
            }
            if (!string.IsNullOrEmpty(simplifyJobsUrl))
            {
                companyInfo.Add($"SimplifyJobs URL: {simplifyJobsUrl}");
            }
            
            if (companyInfo.Count > 0)
            {
                job.Notes = string.Join("; ", companyInfo);
            }
            
            // Extract requirements - store in Summary field
            if (jobData.TryGetProperty("requirements", out JsonElement reqElement) && 
                reqElement.ValueKind == JsonValueKind.Array)
            {
                List<string?> requirements = reqElement.EnumerateArray()
                    .Select(r => r.GetString())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();
                
                if (requirements.Count > 0)
                {
                    job.Summary = string.Join("; ", requirements!);
                }
            }
            
            // Extract skills/technologies
            if (jobData.TryGetProperty("skills", out JsonElement skillsElement) && 
                skillsElement.ValueKind == JsonValueKind.Array)
            {
                List<string?> technologies = skillsElement.EnumerateArray()
                    .Where(s => s.TryGetProperty("name", out _))
                    .Select(s => s.GetProperty("name").GetString())
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();
                
                job.Technologies = technologies!;
            }
            
            // 🚀 Enhanced Benefits field: Show URL type for clarity
            if (!string.IsNullOrEmpty(externalUrl))
            {
                job.Benefits = "✅ Direct apply link available";
            }
            else
            {
                job.Benefits = "⚠️ Apply via SimplifyJobs page";
            }
            
            // Calculate basic match score for .NET roles
            job.MatchScore = CalculateMatchScore(job);
            
            Logger.LogInformation($"🎯 Job converted: {job.Title} at {job.Company} -> URL: {job.Url}");
            
            return job;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error converting job data: {ex.Message}");
            return null;
        }
    }

    private int CalculateMatchScore(EnhancedJobListing job)
    {
        var score = 30; // Base score
        
        string title = job.Title?.ToLower() ?? "";
        string description = job.Description?.ToLower() ?? "";
        string summary = job.Summary?.ToLower() ?? "";
        
        // .NET technology scoring
        if (title.Contains(".net") || description.Contains(".net") || summary.Contains(".net")) score += 20;
        if (title.Contains("c#") || description.Contains("c#") || summary.Contains("c#")) score += 15;
        if (title.Contains("backend") || description.Contains("backend")) score += 10;
        if (title.Contains("senior") || description.Contains("senior")) score += 10;
        if (title.Contains("principal") || title.Contains("lead") || title.Contains("architect")) score += 15;
        
        // Remote work preference
        if (job.IsRemote) score += 10;
        
        // Technology stack bonuses
        List<string> techList = job.Technologies ?? [];
        if (techList.Any(t => t.Contains("Azure", StringComparison.OrdinalIgnoreCase))) score += 5;
        if (techList.Any(t => t.Contains("AWS", StringComparison.OrdinalIgnoreCase))) score += 5;
        if (techList.Any(t => t.Contains("Angular", StringComparison.OrdinalIgnoreCase))) score += 5;
        if (techList.Any(t => t.Contains("SQL", StringComparison.OrdinalIgnoreCase))) score += 5;
        
        return Math.Min(score, 100); // Cap at 100
    }

    private static bool IsValidJobId(string jobId)
    {
        // Basic validation for job ID format (UUIDs, etc.)
        return !string.IsNullOrEmpty(jobId) && 
               jobId.Length is >= 8 and <= 50 &&
               !jobId.Contains(" ");
    }

    [GeneratedRegex(@"/jobs/([a-f0-9-]{8,50})", RegexOptions.IgnoreCase)]
    private static partial Regex JobIdRegex();

    public override SiteConfiguration GetDefaultConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "Simplify.jobs",
            BaseUrl = "https://simplify.jobs",
            Selectors = new Dictionary<string, string>
            {
                ["jobListing"] = "a[href*='/jobs/']",
                ["jobTitle"] = ".job-title, [data-testid*='title']",
                ["companyName"] = ".company-name, [data-testid*='company']"
            },
            RateLimit = new RateLimitConfig
            {
                DelayBetweenRequests = 1000, // Optimized from 2000ms
                RequestsPerMinute = 30 // Optimized from 20
            },
            AntiDetection = new AntiDetectionConfig
            {
                RequiresLogin = true,
                UsesCloudflare = true
            }
        };
    }

    private List<IWebElement> FindJobCardsInLeftPanel()
    {
        var cardSelectors = new[]
        {
            "[data-testid*='job-card']",
            "[data-testid*='job-item']",
            ".job-card",
            ".job-item",
            "button[class*='job']",
            "div[class*='cursor-pointer']"
        };
        
        foreach (string selector in cardSelectors)
        {
            try
            {
                ReadOnlyCollection<IWebElement> elements = Driver!.FindElements(By.CssSelector(selector));
                if (elements.Count > 0)
                {
                    Logger.LogInformation($"Found {elements.Count} job cards with: {selector}");
                    return elements.Take(10).ToList();
                }
            }
            catch { }
        }
        
        return [];
    }
    
    private async Task ClickJobCardSafely(IWebElement jobCard)
    {
        try
        {
            // Scroll into view
            ((IJavaScriptExecutor)Driver!).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", jobCard);
            await Task.Delay(500);
            
            // Click
            jobCard.Click();
            await Task.Delay(1500); // Wait for right panel to update
        }
        catch (Exception)
        {
            // Fallback to JavaScript click
            ((IJavaScriptExecutor)Driver!).ExecuteScript("arguments[0].click();", jobCard);
            await Task.Delay(1500);
        }
    }
    
    private string? ExtractApplyButtonFromRightPanel()
    {
        var applySelectors = new[]
        {
            "button[class*='apply'] a",
            "a[class*='apply']",
            ".apply-button",
            "[data-testid*='apply']"
        };
        
        foreach (string selector in applySelectors)
        {
            try
            {
                IWebElement element = Driver!.FindElement(By.CssSelector(selector));
                string? href = element.GetAttribute("href");
                if (!string.IsNullOrEmpty(href) && href.Contains("simplify.jobs"))
                {
                    return href;
                }
            }
            catch { }
        }
        
        // Try JavaScript approach
        try
        {
            object? jsResult = ((IJavaScriptExecutor)Driver!).ExecuteScript(@"
                const buttons = document.querySelectorAll('button, a');
                for (let btn of buttons) {
                    if (btn.textContent.toLowerCase().includes('apply')) {
                        return btn.href || btn.getAttribute('onclick');
                    }
                }
                return null;
            ");
            
            if (jsResult?.ToString()?.Contains("simplify.jobs") == true)
            {
                return jsResult.ToString();
            }
        }
        catch { }
        
        return null;
    }
    
    private string? ExtractJobIdFromApplyUrl(string url)
    {
        var patterns = new[]
        {
            @"jobs/([a-f0-9-]{8,50})",
            @"jobId=([a-f0-9-]{8,50})",
            @"id=([a-f0-9-]{8,50})"
        };
        
        foreach (string pattern in patterns)
        {
            Match match = Regex.Match(url, pattern, RegexOptions.IgnoreCase);
            if (match.Success && IsValidJobId(match.Groups[1].Value))
            {
                return match.Groups[1].Value;
            }
        }
        
        return null;
    }
    
    private async Task ScrollOneCardHeight()
    {
        try
        {
            var js = (IJavaScriptExecutor)Driver!;
            js.ExecuteScript(@"
                // Find scrollable job container and scroll by one card height
                const containers = document.querySelectorAll('[class*=""job""], [class*=""list""]');
                let scrollTarget = null;
                
                for (let container of containers) {
                    const style = window.getComputedStyle(container);
                    if (style.overflowY === 'scroll' || style.overflowY === 'auto') {
                        scrollTarget = container;
                        break;
                    }
                }
                
                if (scrollTarget) {
                    scrollTarget.scrollTop += 100; // One card height
                } else {
                    window.scrollBy(0, 100);
                }
            ");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Scroll error: {ex.Message}");
        }
    }

}