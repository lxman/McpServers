using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Playwright.Core.Services;

namespace PlaywrightServer.Controllers;

/// <summary>
/// Advanced testing capabilities including network simulation, file uploads, and database testing
/// </summary>
[ApiController]
[Route("api/advanced")]
public class AdvancedTestingController(
    PlaywrightSessionManager sessionManager,
    ToolService toolService,
    ILogger<AdvancedTestingController> logger)
    : ControllerBase
{
    /// <summary>
    /// Simulate various network conditions
    /// </summary>
    [HttpPost("network-conditions")]
    public async Task<IActionResult> SimulateNetworkConditions([FromBody] NetworkConditionsRequest request)
    {
        try
        {
            IPage? page = toolService.GetPage(request.SessionId ?? "default");
            if (page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            ICDPSession cdp = await page.Context.NewCDPSessionAsync(page);

            var conditions = request.NetworkType?.ToLower() switch
            {
                "slow" => new { offline = false, downloadThroughput = 50 * 1024, uploadThroughput = 20 * 1024, latency = 500 },
                "mobile3g" => new { offline = false, downloadThroughput = 100 * 1024, uploadThroughput = 50 * 1024, latency = 300 },
                "mobile4g" => new { offline = false, downloadThroughput = 1 * 1024 * 1024, uploadThroughput = 500 * 1024, latency = 150 },
                "fast" => new { offline = false, downloadThroughput = 10 * 1024 * 1024, uploadThroughput = 5 * 1024 * 1024, latency = 10 },
                "offline" => new { offline = true, downloadThroughput = 0, uploadThroughput = 0, latency = 0 },
                _ => new { offline = false, downloadThroughput = 1 * 1024 * 1024, uploadThroughput = 500 * 1024, latency = 50 }
            };

            await cdp.SendAsync("Network.emulateNetworkConditions", new Dictionary<string, object>
            {
                ["offline"] = conditions.offline,
                ["downloadThroughput"] = conditions.downloadThroughput,
                ["uploadThroughput"] = conditions.uploadThroughput,
                ["latency"] = conditions.latency
            });

            return Ok(new
            {
                success = true,
                networkType = request.NetworkType,
                downloadSpeed = $"{conditions.downloadThroughput / 1024}KB/s",
                uploadSpeed = $"{conditions.uploadThroughput / 1024}KB/s",
                latency = $"{conditions.latency}ms"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error simulating network conditions");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Emulate geolocation
    /// </summary>
    [HttpPost("geolocation")]
    public async Task<IActionResult> SetGeolocation([FromBody] GeolocationRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Context == null)
                return BadRequest(new { success = false, error = "Session not found" });

            await session.Context.SetGeolocationAsync(new()
            {
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Accuracy = request.Accuracy
            });

            return Ok(new
            {
                success = true,
                location = new
                {
                    latitude = request.Latitude,
                    longitude = request.Longitude,
                    accuracy = request.Accuracy
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting geolocation");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Set custom HTTP headers
    /// </summary>
    [HttpPost("headers")]
    public async Task<IActionResult> SetHeaders([FromBody] HeadersRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Context == null)
                return BadRequest(new { success = false, error = "Session not found" });

            if (request.Headers != null)
            {
                await session.Context.SetExtraHTTPHeadersAsync(request.Headers);
            }

            return Ok(new
            {
                success = true,
                headersSet = request.Headers?.Count ?? 0,
                headers = request.Headers
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting headers");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Intercept and mock API responses
    /// </summary>
    [HttpPost("mock-api")]
    public async Task<IActionResult> MockApiResponse([FromBody] MockApiRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            await session.Page.RouteAsync(request.UrlPattern, async route =>
            {
                await route.FulfillAsync(new()
                {
                    Status = request.StatusCode,
                    ContentType = request.ContentType ?? "application/json",
                    Body = request.ResponseBody
                });
            });

            return Ok(new
            {
                success = true,
                message = $"API mock set up for pattern: {request.UrlPattern}",
                statusCode = request.StatusCode
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error mocking API");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Wait for specific network request
    /// </summary>
    [HttpPost("wait-for-request")]
    public async Task<IActionResult> WaitForRequest([FromBody] WaitForRequestRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            Task<IRequest> requestTask = session.Page.WaitForRequestAsync(request.UrlPattern, new()
            {
                Timeout = request.TimeoutMs
            });

            IRequest req = await requestTask;

            return Ok(new
            {
                success = true,
                url = req.Url,
                method = req.Method,
                headers = req.Headers,
                resourceType = req.ResourceType
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error waiting for request");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Wait for specific network response
    /// </summary>
    [HttpPost("wait-for-response")]
    public async Task<IActionResult> WaitForResponse([FromBody] WaitForResponseRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            Task<IResponse> responseTask = session.Page.WaitForResponseAsync(request.UrlPattern, new()
            {
                Timeout = request.TimeoutMs
            });

            IResponse response = await responseTask;

            return Ok(new
            {
                success = true,
                url = response.Url,
                status = response.Status,
                statusText = response.StatusText,
                headers = response.Headers
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error waiting for response");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Execute multiple actions in sequence
    /// </summary>
    [HttpPost("batch-actions")]
    public async Task<IActionResult> BatchActions([FromBody] BatchActionsRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var results = new List<object>();

            foreach (string action in request.Actions ?? new List<string>())
            {
                try
                {
                    // Simple action parser - expand based on needs
                    if (action.StartsWith("click:"))
                    {
                        string selector = action.Substring(6);
                        await session.Page.ClickAsync(selector);
                        results.Add(new { action = "click", selector, success = true });
                    }
                    else if (action.StartsWith("fill:"))
                    {
                        string[] parts = action.Substring(5).Split('=');
                        if (parts.Length == 2)
                        {
                            await session.Page.FillAsync(parts[0], parts[1]);
                            results.Add(new { action = "fill", selector = parts[0], success = true });
                        }
                    }
                    else if (action.StartsWith("wait:"))
                    {
                        int ms = int.Parse(action.Substring(5));
                        await Task.Delay(ms);
                        results.Add(new { action = "wait", duration = ms, success = true });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { action, success = false, error = ex.Message });
                }
            }

            return Ok(new { success = true, results });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing batch actions");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public record NetworkConditionsRequest(string? NetworkType = "fast", string? SessionId = "default");
public record GeolocationRequest(float Latitude, float Longitude, float Accuracy = 0, string? SessionId = "default");
public record HeadersRequest(Dictionary<string, string>? Headers, string? SessionId = "default");
public record MockApiRequest(string UrlPattern, int StatusCode, string? ContentType, string ResponseBody, string? SessionId = "default");
public record WaitForRequestRequest(string UrlPattern, float TimeoutMs = 30000, string? SessionId = "default");
public record WaitForResponseRequest(string UrlPattern, float TimeoutMs = 30000, string? SessionId = "default");
public record BatchActionsRequest(List<string>? Actions, string? SessionId = "default");