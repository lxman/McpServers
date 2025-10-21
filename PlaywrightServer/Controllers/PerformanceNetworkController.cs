using Microsoft.AspNetCore.Mvc;
using Playwright.Core.Models;
using Playwright.Core.Services;

namespace PlaywrightServer.Controllers;

/// <summary>
/// Performance and network testing endpoints
/// </summary>
[ApiController]
[Route("api/performance")]
public class PerformanceNetworkController(
    PlaywrightSessionManager sessionManager,
    ILogger<PerformanceNetworkController> logger)
    : ControllerBase
{
    /// <summary>
    /// Measure page load performance metrics
    /// </summary>
    [HttpPost("measure-load")]
    public async Task<IActionResult> MeasureLoadTime([FromBody] MeasureLoadRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var metrics = await session.Page.EvaluateAsync<object>("""

                                                                                   () => {
                                                                                       const timing = performance.timing;
                                                                                       const navigation = performance.getEntriesByType('navigation')[0];
                                                                                       return {
                                                                                           navigationStart: timing.navigationStart,
                                                                                           domContentLoaded: timing.domContentLoadedEventEnd - timing.navigationStart,
                                                                                           loadComplete: timing.loadEventEnd - timing.navigationStart,
                                                                                           domInteractive: timing.domInteractive - timing.navigationStart,
                                                                                           firstPaint: navigation ? navigation.responseStart : 0,
                                                                                           dns: timing.domainLookupEnd - timing.domainLookupStart,
                                                                                           tcp: timing.connectEnd - timing.connectStart,
                                                                                           request: timing.responseStart - timing.requestStart,
                                                                                           response: timing.responseEnd - timing.responseStart,
                                                                                           processing: timing.domComplete - timing.domLoading,
                                                                                           onLoad: timing.loadEventEnd - timing.loadEventStart
                                                                                       };
                                                                                   }
                                                                   """);

            return Ok(new { success = true, metrics });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error measuring page load");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Monitor network requests
    /// </summary>
    [HttpGet("network-activity")]
    public IActionResult GetNetworkActivity([FromQuery] string? sessionId = "default", [FromQuery] string? urlFilter = null)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId ?? "default");
            if (session == null)
                return BadRequest(new { success = false, error = "Session not found" });

            IEnumerable<NetworkLogEntry> logs = session.NetworkLogs.AsEnumerable();
            if (!string.IsNullOrEmpty(urlFilter))
            {
                logs = logs.Where(log => log.Url.Contains(urlFilter, StringComparison.OrdinalIgnoreCase));
            }

            var result = logs.OrderByDescending(l => l.Timestamp).Take(100).Select(log => new
            {
                log.Type,
                log.Method,
                log.Url,
                log.Status,
                log.Duration,
                Timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            }).ToList();

            return Ok(new { success = true, count = result.Count, requests = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get console logs from browser
    /// </summary>
    [HttpGet("console-logs")]
    public IActionResult GetConsoleLogs(
        [FromQuery] string? sessionId = "default",
        [FromQuery] string? logType = null,
        [FromQuery] int maxLogs = 100)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId ?? "default");
            if (session == null)
                return BadRequest(new { success = false, error = "Session not found" });

            IEnumerable<ConsoleLogEntry> logs = session.ConsoleLogs.AsEnumerable();
            if (!string.IsNullOrEmpty(logType))
            {
                logs = logs.Where(log => log.Type.Equals(logType, StringComparison.OrdinalIgnoreCase));
            }

            var result = logs.OrderByDescending(l => l.Timestamp).Take(maxLogs).Select(log => new
            {
                log.Type,
                log.Text,
                Timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                log.IsError,
                log.IsWarning
            });

            return Ok(new { success = true, count = result.Count(), logs = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Clear session logs
    /// </summary>
    [HttpPost("clear-logs")]
    public IActionResult ClearLogs([FromBody] ClearLogsRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var clearedItems = new List<string>();

            if (request.ClearConsole)
            {
                int consoleLogsCount = session.ConsoleLogs.Count;
                session.ConsoleLogs.Clear();
                clearedItems.Add($"{consoleLogsCount} console logs");
            }

            if (!request.ClearNetwork)
                return Ok(new { success = true, message = $"Cleared {string.Join(" and ", clearedItems)}" });
            int networkLogsCount = session.NetworkLogs.Count;
            session.NetworkLogs.Clear();
            clearedItems.Add($"{networkLogsCount} network logs");

            return Ok(new { success = true, message = $"Cleared {string.Join(" and ", clearedItems)}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get session debug summary
    /// </summary>
    [HttpGet("session-summary")]
    public IActionResult GetSessionSummary([FromQuery] string? sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId ?? "default");
            if (session == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var summary = new
            {
                sessionId,
                isActive = session.IsActive,
                createdAt = session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                consoleLogs = new
                {
                    total = session.ConsoleLogs.Count,
                    errors = session.ConsoleLogs.Count(l => l.IsError),
                    warnings = session.ConsoleLogs.Count(l => l.IsWarning)
                },
                networkLogs = new
                {
                    total = session.NetworkLogs.Count,
                    apiCalls = session.NetworkLogs.Count(l => l.IsApiCall),
                    authRelated = session.NetworkLogs.Count(l => l.IsAuthRelated)
                },
                activeSessions = sessionManager.GetActiveSessionIds()
            };

            return Ok(new { success = true, summary });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Measure Core Web Vitals
    /// </summary>
    [HttpPost("web-vitals")]
    public async Task<IActionResult> MeasureWebVitals([FromBody] WebVitalsRequest request)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(request.SessionId ?? "default");
            if (session?.Page == null)
                return BadRequest(new { success = false, error = "Session not found" });

            var vitals = await session.Page.EvaluateAsync<object>("""

                                                                                  () => {
                                                                                      return new Promise((resolve) => {
                                                                                          const vitals = {};
                                                                                          if (window.performance && window.performance.getEntriesByType) {
                                                                                              const paintEntries = performance.getEntriesByType('paint');
                                                                                              const fcp = paintEntries.find(e => e.name === 'first-contentful-paint');
                                                                                              if (fcp) vitals.fcp = fcp.startTime;
                                                                                              
                                                                                              const navTiming = performance.getEntriesByType('navigation')[0];
                                                                                              if (navTiming) {
                                                                                                  vitals.ttfb = navTiming.responseStart;
                                                                                                  vitals.domContentLoaded = navTiming.domContentLoadedEventEnd;
                                                                                                  vitals.loadComplete = navTiming.loadEventEnd;
                                                                                              }
                                                                                          }
                                                                                          setTimeout(() => resolve(vitals), 100);
                                                                                      });
                                                                                  }
                                                                  """);

            return Ok(new { success = true, vitals });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public record MeasureLoadRequest(string? SessionId = "default");
public record ClearLogsRequest(bool ClearConsole = true, bool ClearNetwork = true, string? SessionId = "default");
public record WebVitalsRequest(string? SessionId = "default");