using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services;

public abstract class BaseJobScraper : IJobSiteScraper, IDisposable
{
    protected IWebDriver? Driver;
    protected readonly ILogger Logger;
    protected readonly Random Random = new();
    private bool _disposed;
    
    public abstract JobSite SupportedSite { get; }

    protected BaseJobScraper(ILogger logger)
    {
        Logger = logger;
    }

    public abstract Task<List<EnhancedJobListing>> ScrapeJobsAsync(EnhancedScrapeRequest request, SiteConfiguration config);
    public abstract SiteConfiguration GetDefaultConfiguration();

    public virtual async Task<bool> TestSiteAccessibilityAsync(SiteConfiguration config)
    {
        try
        {
            InitializeDriver(config.AntiDetection);
            Driver!.Navigate().GoToUrl(config.BaseUrl);
            await Task.Delay(2000);
            return Driver.Title.Length > 0;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Site accessibility test failed for {config.SiteName}: {ex.Message}");
            return false;
        }
    }

    protected void InitializeDriver(AntiDetectionConfig antiDetection)
    {
        if (Driver != null) return;

        var options = new ChromeOptions();
        
        // Enhanced anti-detection measures based on LinkedIn scraper analysis
        options.AddArgument("--headless=new"); // Use new headless mode
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        
        // LinkedIn-specific anti-detection
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--disable-web-security");
        options.AddArgument("--disable-features=VizDisplayCompositor");
        options.AddArgument("--disable-extensions");
        options.AddArgument("--no-first-run");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-default-apps");
        options.AddArgument("--disable-popup-blocking");
        
        // Remove automation indicators
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        
        // Set realistic preferences
        options.AddUserProfilePreference("credentials_enable_service", false);
        options.AddUserProfilePreference("profile.password_manager_enabled", false);
        
        // Try to find Chrome binary path
        string[] chromePaths =
        [
            antiDetection.ChromeBinaryPath,
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Google\Chrome\Application\chrome.exe"
        ];
        
        foreach (string path in chromePaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            if (!File.Exists(path)) continue;
            options.BinaryLocation = path;
            Logger.LogInformation($"Using Chrome binary at: {path}");
            break;
        }
        
        // Enhanced user agent rotation for LinkedIn
        if (antiDetection.UserAgents.Count != 0)
        {
            string userAgent = antiDetection.UserAgents[Random.Next(antiDetection.UserAgents.Count)];
            options.AddArgument($"--user-agent={userAgent}");
            Logger.LogInformation("Using User-Agent: {UserAgent}", userAgent.Substring(0, Math.Min(50, userAgent.Length)) + "...");
        }
        
        try
        {
            Driver = new ChromeDriver(options);
            
            // Enhanced timeouts for LinkedIn's heavy JavaScript
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            Driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            Driver.Manage().Timeouts().AsynchronousJavaScript = TimeSpan.FromSeconds(10);
            
            // Execute enhanced anti-detection scripts
            ExecuteAntiDetectionScripts();
            
            Logger.LogInformation("Chrome driver initialized successfully with enhanced anti-detection");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize Chrome driver: {ex.Message}");
            throw;
        }
    }

    private void ExecuteAntiDetectionScripts()
    {
        try
        {
            var jsExecutor = (IJavaScriptExecutor)Driver!;
            
            // Hide webdriver property
            jsExecutor.ExecuteScript("Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
            
            // Override automation-related properties
            jsExecutor.ExecuteScript(@"
                Object.defineProperty(navigator, 'plugins', {
                    get: () => [1, 2, 3, 4, 5]
                });
            ");
            
            jsExecutor.ExecuteScript(@"
                Object.defineProperty(navigator, 'languages', {
                    get: () => ['en-US', 'en']
                });
            ");
            
            // Override chrome runtime
            jsExecutor.ExecuteScript(@"
                if (window.chrome) {
                    window.chrome.runtime = undefined;
                }
            ");
            
            // Add realistic screen properties
            jsExecutor.ExecuteScript(@"
                Object.defineProperty(screen, 'width', {get: () => 1920});
                Object.defineProperty(screen, 'height', {get: () => 1080});
                Object.defineProperty(screen, 'availWidth', {get: () => 1920});
                Object.defineProperty(screen, 'availHeight', {get: () => 1040});
            ");
            
            Logger.LogInformation("Anti-detection scripts executed successfully");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Warning: Could not execute all anti-detection scripts: {ex.Message}");
        }
    }

    protected async Task RespectRateLimit(RateLimitConfig rateLimit)
    {
        int delay = Random.Next(rateLimit.DelayBetweenRequests, rateLimit.DelayBetweenRequests + 2000);
        await Task.Delay(delay);
    }

    protected async Task<T?> RetryOperation<T>(Func<Task<T>> operation, RateLimitConfig rateLimit)
    {
        for (var attempt = 1; attempt <= rateLimit.RetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Attempt {attempt} failed: {ex.Message}");
                if (attempt == rateLimit.RetryAttempts) throw;
                await Task.Delay(rateLimit.RetryDelay * attempt);
            }
        }
        return default;
    }

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                if (Driver != null)
                {
                    Logger.LogInformation("Shutting down Chrome driver gracefully...");
                    
                    try
                    {
                        // Quit the driver (this should close all Chrome processes)
                        Driver.Quit();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error quitting Chrome driver: {ex.Message}");
                    }
                    
                    try
                    {
                        // Dispose the driver
                        Driver.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error disposing Chrome driver: {ex.Message}");
                    }
                    
                    Driver = null;
                    Logger.LogInformation("Chrome driver shutdown completed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error during Chrome driver shutdown: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    ~BaseJobScraper()
    {
        Dispose(false);
    }
}