using Microsoft.AspNetCore.Mvc;
using PlaywrightServer.Models;
using PlaywrightServer.Services;

namespace PlaywrightServer.Controllers;

/// <summary>
/// Core Playwright browser testing and automation endpoints
/// </summary>
[ApiController]
[Route("api/core")]
public class CoreTestingController(
    PlaywrightSessionManager sessionManager,
    ToolService toolService,
    ILogger<CoreTestingController> logger)
    : ControllerBase
{
    /// <summary>
    /// Launch a browser and create a new session
    /// </summary>
    [HttpPost("launch-browser")]
    public async Task<IActionResult> LaunchBrowser([FromBody] LaunchBrowserRequest request)
    {
        try
        {
            var browserOptions = new BrowserLaunchOptions
            {
                ViewportWidth = request.ViewportWidth,
                ViewportHeight = request.ViewportHeight,
                DeviceEmulation = request.DeviceEmulation,
                UserAgent = request.UserAgent,
                Timezone = request.Timezone,
                Locale = request.Locale,
                ColorScheme = request.ColorScheme,
                ReducedMotion = request.ReducedMotion,
                EnableGeolocation = request.EnableGeolocation,
                EnableCamera = request.EnableCamera,
                EnableMicrophone = request.EnableMicrophone,
                ExtraHttpHeaders = request.ExtraHttpHeaders
            };

            string result = await sessionManager.CreateSessionAsync(
                request.SessionId ?? "default",
                request.BrowserType ?? "chrome",
                request.Headless,
                browserOptions);

            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session != null)
            {
                if (session.Browser != null) toolService.StoreBrowser(request.SessionId ?? "default", session.Browser);
                if (session.Context != null) toolService.StoreBrowserContext(request.SessionId ?? "default", session.Context);
                if (session.Page != null) toolService.StorePage(request.SessionId ?? "default", session.Page);
            }

            return Ok(new { success = true, message = result, sessionId = request.SessionId ?? "default" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error launching browser");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Navigate to a URL
    /// </summary>
    [HttpPost("navigate")]
    public async Task<IActionResult> NavigateToUrl([FromBody] NavigateRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = $"Session {request.SessionId} not found or page not available" });

            await session.Page.GotoAsync(request.Url);
            return Ok(new { success = true, message = $"Successfully navigated to {request.Url}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Navigation failed");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Fill a form field
    /// </summary>
    [HttpPost("fill-field")]
    public async Task<IActionResult> FillField([FromBody] FillFieldRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            string selector = DetermineSelector(request.Selector);
            await session.Page.Locator(selector).FillAsync(request.Value);
            return Ok(new { success = true, message = $"Field {request.Selector} filled" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Click an element
    /// </summary>
    [HttpPost("click")]
    public async Task<IActionResult> ClickElement([FromBody] ClickElementRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            string selector = DetermineSelector(request.Selector);
            await session.Page.Locator(selector).ClickAsync();
            return Ok(new { success = true, message = $"Clicked {request.Selector}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Execute JavaScript on the page
    /// </summary>
    [HttpPost("execute-js")]
    public async Task<IActionResult> ExecuteJavaScript([FromBody] ExecuteJsRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var result = await session.Page.EvaluateAsync<object>(request.JsCode);
            return Ok(new { success = true, result = result?.ToString() ?? "null" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get active sessions
    /// </summary>
    [HttpGet("sessions")]
    public IActionResult GetSessions()
    {
        try
        {
            IEnumerable<string> sessions = sessionManager.GetActiveSessionIds();
            return Ok(new { success = true, sessions });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Close a browser session
    /// </summary>
    [HttpPost("close-session")]
    public async Task<IActionResult> CloseSession([FromBody] CloseSessionRequest request)
    {
        try
        {
            await sessionManager.CloseSessionAsync(request.SessionId ?? "default");
            return Ok(new { success = true, message = $"Session {request.SessionId} closed" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Take a screenshot and save to file
    /// </summary>
    [HttpPost("screenshot")]
    public async Task<IActionResult> TakeScreenshot([FromBody] FileScreenshotRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            byte[] bytes = await session.Page.ScreenshotAsync(new()
            {
                Path = request.FilePath,
                FullPage = request.FullPage
            });
            return Ok(new { success = true, message = $"Screenshot saved to {request.FilePath}", size = bytes.Length });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private static string DetermineSelector(string selector)
    {
        if (selector.StartsWith('#') || selector.StartsWith('.') || selector.Contains('['))
            return selector;
        return $"[data-testid='{selector}']";
    }
}

// Request models
public record LaunchBrowserRequest(
    string? BrowserType = "chrome",
    bool Headless = true,
    string? SessionId = "default",
    int ViewportWidth = 1920,
    int ViewportHeight = 1080,
    string? DeviceEmulation = null,
    string? UserAgent = null,
    string? Timezone = null,
    string? Locale = null,
    string? ColorScheme = null,
    string? ReducedMotion = null,
    bool EnableGeolocation = false,
    bool EnableCamera = false,
    bool EnableMicrophone = false,
    string? ExtraHttpHeaders = null);

public record NavigateRequest(string Url, string? SessionId = "default");
public record FillFieldRequest(string Selector, string Value, string? SessionId = "default");
public record ClickElementRequest(string Selector, string? SessionId = "default");
public record ExecuteJsRequest(string JsCode, string? SessionId = "default");
public record CloseSessionRequest(string? SessionId = "default");
public record FileScreenshotRequest(string FilePath, bool FullPage = false, string? SessionId = "default");