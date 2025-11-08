using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using ModelContextProtocol.Server;
using Playwright.Core.Models;
using Playwright.Core.Services;
using PlaywrightServerMcp.Common;
using PlaywrightServerMcp.Models;

namespace PlaywrightServerMcp.Tools;

[McpServerToolType]
public partial class NetworkTestingTools(PlaywrightSessionManager sessionManager)
{
    // Track active downloads per session
    private static readonly ConcurrentDictionary<string, List<DownloadInfo>> ActiveDownloads = new();
    private static readonly ConcurrentDictionary<string, List<MockRule>> MockRules = new();
    private static readonly ConcurrentDictionary<string, List<InterceptRule>> InterceptRules = new();

    [McpServerTool]
    [Description("Mock API responses for testing with advanced features. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> MockApiResponse(
        string urlPattern,
        string responseBody,
        int statusCode = 200,
        string method = "GET",
        string? headers = null,
        int delayMs = 0,
        string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            // Parse headers if provided
            var responseHeaders = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(headers))
            {
                try
                {
                    var headerDict = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
                    if (headerDict != null)
                        responseHeaders = headerDict;
                }
                catch
                {
                    return "Invalid headers JSON format";
                }
            }

            // Add default content-type if not specified
            if (!responseHeaders.ContainsKey("content-type"))
            {
                if (responseBody.TrimStart().StartsWith("{") || responseBody.TrimStart().StartsWith("["))
                    responseHeaders["content-type"] = "application/json";
                else if (responseBody.TrimStart().StartsWith("<"))
                    responseHeaders["content-type"] = "application/xml";
                else
                    responseHeaders["content-type"] = "text/plain";
            }

            var mockRule = new MockRule
            {
                UrlPattern = urlPattern,
                Method = method.ToUpper(),
                ResponseBody = responseBody,
                StatusCode = statusCode,
                Headers = responseHeaders,
                Delay = delayMs > 0 ? TimeSpan.FromMilliseconds(delayMs) : null
            };

            // Store the mock rule
            MockRules.AddOrUpdate(sessionId, 
                [mockRule], 
                (key, existing) => { existing.Add(mockRule); return existing; });

            // Set up the route
            await session.Page.RouteAsync(urlPattern, async route =>
            {
                var request = route.Request;
                
                // Check if method matches
                if (mockRule.Method != "*" && request.Method.ToUpper() != mockRule.Method)
                {
                    await route.ContinueAsync();
                    return;
                }

                // Check if the rule is still active
                if (!mockRule.IsActive)
                {
                    await route.ContinueAsync();
                    return;
                }

                // Apply delay if specified
                if (mockRule.Delay.HasValue)
                {
                    await Task.Delay(mockRule.Delay.Value);
                }

                // Increment usage counter
                mockRule.TimesUsed++;

                // Fulfill the request with the mock response
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = mockRule.StatusCode,
                    Headers = mockRule.Headers,
                    Body = mockRule.ResponseBody
                });
            });

            var result = new
            {
                success = true,
                mockRuleId = mockRule.Id,
                urlPattern,
                method,
                statusCode,
                hasDelay = delayMs > 0,
                delayMs,
                headers = responseHeaders,
                bodyLength = responseBody.Length,
                sessionId,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to mock API response: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Intercept and modify network requests. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> InterceptRequests(
        string urlPattern,
        string action = "block",
        string method = "*",
        string? modifiedBody = null,
        string? modifiedHeaders = null,
        int? modifiedStatusCode = null,
        int delayMs = 1000,
        string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            // Parse modified headers if provided
            var headerDict = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(modifiedHeaders))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(modifiedHeaders);
                    if (parsed != null)
                        headerDict = parsed;
                }
                catch
                {
                    return "Invalid modified headers JSON format";
                }
            }

            var interceptRule = new InterceptRule
            {
                UrlPattern = urlPattern,
                Method = method.ToUpper(),
                Action = action.ToLower(),
                ModifiedBody = modifiedBody,
                ModifiedHeaders = headerDict,
                ModifiedStatusCode = modifiedStatusCode
            };

            // Store the intercept rule
            InterceptRules.AddOrUpdate(sessionId,
                [interceptRule],
                (key, existing) => { existing.Add(interceptRule); return existing; });

            // Set up the route
            await session.Page.RouteAsync(urlPattern, async route =>
            {
                var request = route.Request;
                
                // Check if the method matches
                if (interceptRule.Method != "*" && request.Method.ToUpper() != interceptRule.Method)
                {
                    await route.ContinueAsync();
                    return;
                }

                // Check if the rule is still active
                if (!interceptRule.IsActive)
                {
                    await route.ContinueAsync();
                    return;
                }

                // Increment trigger counter
                interceptRule.TimesTriggered++;

                switch (interceptRule.Action)
                {
                    case "block":
                        await route.AbortAsync();
                        break;
                        
                    case "modify":
                        // Get the original response first
                        var response = await route.FetchAsync();
                        var originalBody = await response.TextAsync();
                        
                        await route.FulfillAsync(new RouteFulfillOptions
                        {
                            Status = interceptRule.ModifiedStatusCode ?? response.Status,
                            Headers = interceptRule.ModifiedHeaders.Count > 0 ? interceptRule.ModifiedHeaders : response.Headers,
                            Body = interceptRule.ModifiedBody ?? originalBody
                        });
                        break;
                        
                    case "delay":
                        await Task.Delay(delayMs);
                        await route.ContinueAsync();
                        break;
                        
                    case "log":
                        // Log the request details but continue normally
                        var requestInfo = new
                        {
                            url = request.Url,
                            method = request.Method,
                            headers = request.Headers,
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                        };
                        
                        // Store in session logs or console
                        session.ConsoleLogs.Add(new ConsoleLogEntry
                        {
                            Type = "network-intercept",
                            Text = $"Intercepted request: {JsonSerializer.Serialize(requestInfo)}",
                            Timestamp = DateTime.UtcNow
                        });
                        
                        await route.ContinueAsync();
                        break;
                        
                    default:
                        await route.ContinueAsync();
                        break;
                }
            });

            var result = new
            {
                success = true,
                interceptRuleId = interceptRule.Id,
                urlPattern,
                method,
                action,
                hasModifications = !string.IsNullOrEmpty(modifiedBody) || modifiedStatusCode.HasValue || headerDict.Count > 0,
                sessionId,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to intercept requests: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Handle file downloads with support for multiple concurrent downloads. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> WaitForDownload(
        string triggerSelector,
        int timeoutSeconds = 30,
        string? expectedFileName = null,
        string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var finalSelector = DetermineSelector(triggerSelector);
            var element = session.Page.Locator(finalSelector);
            
            // Check if the element exists
            var count = await element.CountAsync();
            if (count == 0)
            {
                return JsonSerializer.Serialize(new { 
                    success = false, 
                    error = "Trigger element not found", 
                    selector = finalSelector 
                });
            }

            var downloadInfo = new DownloadInfo
            {
                TriggerSelector = finalSelector,
                StartTime = DateTime.UtcNow
            };

            // Set up the download directory
            var downloadDir = Path.Combine(Directory.GetCurrentDirectory(), "downloads", sessionId);
            Directory.CreateDirectory(downloadDir);

            // Set up download handling
            var downloadTcs = new TaskCompletionSource<IDownload>();
            
            session.Page.Download += (_, download) =>
            {
                downloadTcs.TrySetResult(download);
            };

            try
            {
                // Trigger the download
                await element.ClickAsync();
                
                // Wait for download to start
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                var completedTask = await Task.WhenAny(downloadTcs.Task, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    downloadInfo.Status = "failed";
                    downloadInfo.Error = "Download timeout - no download started";
                    
                    return JsonSerializer.Serialize(new { 
                        success = false, 
                        error = "Download timeout",
                        downloadInfo 
                    });
                }

                var download = await downloadTcs.Task;
                
                // Update download info
                downloadInfo.FileName = download.SuggestedFilename;
                downloadInfo.LocalPath = Path.Combine(downloadDir, downloadInfo.FileName);
                
                // Verify the expected filename if provided
                if (!string.IsNullOrEmpty(expectedFileName) && !downloadInfo.FileName.Contains(expectedFileName))
                {
                    downloadInfo.Status = "warning";
                    downloadInfo.Error = $"Filename mismatch. Expected: {expectedFileName}, Got: {downloadInfo.FileName}";
                }

                // Save the download
                await download.SaveAsAsync(downloadInfo.LocalPath);
                
                // Get file info
                var fileInfo = new FileInfo(downloadInfo.LocalPath);
                downloadInfo.Size = fileInfo.Length;
                downloadInfo.CompletedTime = DateTime.UtcNow;
                downloadInfo.Status = downloadInfo.Status == "warning" ? "warning" : "completed";

                // Track the download
                ActiveDownloads.AddOrUpdate(sessionId,
                    [downloadInfo],
                    (key, existing) => { existing.Add(downloadInfo); return existing; });

                var result = new
                {
                    success = true,
                    downloadId = downloadInfo.Id,
                    fileName = downloadInfo.FileName,
                    localPath = downloadInfo.LocalPath,
                    size = downloadInfo.Size,
                    downloadTime = (downloadInfo.CompletedTime - downloadInfo.StartTime)?.TotalSeconds ?? 0,
                    status = downloadInfo.Status,
                    error = downloadInfo.Error,
                    triggerSelector = finalSelector,
                    sessionId,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };

                return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
            }
            catch (Exception ex)
            {
                downloadInfo.Status = "failed";
                downloadInfo.Error = ex.Message;
                
                return $"Download failed: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            return $"Failed to handle download: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Clean up downloaded files and remove tracking. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> CleanupDownloads(
        string? downloadId = null,
        bool deleteFiles = true,
        string sessionId = "default")
    {
        try
        {
            if (!ActiveDownloads.TryGetValue(sessionId, out var downloads))
            {
                return JsonSerializer.Serialize(new { 
                    success = true, 
                    message = "No downloads found for session",
                    cleanedCount = 0
                });
            }

            var toRemove = new List<DownloadInfo>();
            var cleanedFiles = new List<object>();
            var errors = new List<string>();

            if (!string.IsNullOrEmpty(downloadId))
            {
                // Clean the specific download
                var download = downloads.FirstOrDefault(d => d.Id == downloadId);
                if (download != null)
                {
                    toRemove.Add(download);
                }
                else
                {
                    return JsonSerializer.Serialize(new { 
                        success = false, 
                        error = "Download ID not found" 
                    });
                }
            }
            else
            {
                // Clean all downloads for the session
                toRemove.AddRange(downloads);
            }

            foreach (var download in toRemove)
            {
                try
                {
                    var fileDeleted = false;
                    if (deleteFiles && File.Exists(download.LocalPath))
                    {
                        File.Delete(download.LocalPath);
                        fileDeleted = true;
                    }

                    cleanedFiles.Add(new
                    {
                        downloadId = download.Id,
                        fileName = download.FileName,
                        localPath = download.LocalPath,
                        size = download.Size,
                        fileDeleted,
                        status = download.Status
                    });

                    downloads.Remove(download);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to clean {download.FileName}: {ex.Message}");
                }
            }

            // Clean up empty session entry
            if (downloads.Count == 0)
            {
                ActiveDownloads.TryRemove(sessionId, out _);
            }

            var result = new
            {
                success = true,
                cleanedCount = cleanedFiles.Count,
                cleanedFiles,
                errors,
                remainingDownloads = downloads.Count,
                sessionId,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to cleanup downloads: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List all active downloads for a session. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> ListActiveDownloads(
        string sessionId = "default")
    {
        try
        {
            var downloads = ActiveDownloads.GetValueOrDefault(sessionId, []);
            
            var result = new
            {
                success = true,
                sessionId,
                downloadCount = downloads.Count,
                downloads = downloads.Select(d => new
                {
                    id = d.Id,
                    fileName = d.FileName,
                    localPath = d.LocalPath,
                    size = d.Size,
                    status = d.Status,
                    startTime = d.StartTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    completedTime = d.CompletedTime?.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    duration = d.CompletedTime.HasValue ? 
                              (d.CompletedTime.Value - d.StartTime).TotalSeconds : (double?)null,
                    triggerSelector = d.TriggerSelector,
                    error = d.Error
                }).ToList(),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to list downloads: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List and manage active mock rules. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> ListMockRules(
        string sessionId = "default")
    {
        try
        {
            var mockRules = MockRules.GetValueOrDefault(sessionId, []);
            
            var result = new
            {
                success = true,
                sessionId,
                ruleCount = mockRules.Count,
                mockRules = mockRules.Select(r => new
                {
                    id = r.Id,
                    urlPattern = r.UrlPattern,
                    method = r.Method,
                    statusCode = r.StatusCode,
                    responseBodyLength = r.ResponseBody.Length,
                    hasDelay = r.Delay.HasValue,
                    delayMs = r.Delay?.TotalMilliseconds,
                    isActive = r.IsActive,
                    timesUsed = r.TimesUsed,
                    createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }).ToList(),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to list mock rules: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("List and manage active intercept rules. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> ListInterceptRules(
        string sessionId = "default")
    {
        try
        {
            var interceptRules = InterceptRules.GetValueOrDefault(sessionId, []);
            
            var result = new
            {
                success = true,
                sessionId,
                ruleCount = interceptRules.Count,
                interceptRules = interceptRules.Select(r => new
                {
                    id = r.Id,
                    urlPattern = r.UrlPattern,
                    method = r.Method,
                    action = r.Action,
                    hasModifications = !string.IsNullOrEmpty(r.ModifiedBody) || 
                                     r.ModifiedStatusCode.HasValue || 
                                     r.ModifiedHeaders.Count > 0,
                    isActive = r.IsActive,
                    timesTriggered = r.TimesTriggered,
                    createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
                }).ToList(),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to list intercept rules: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Generate HAR file for network analysis. See skills/playwright-mcp/tools/network-testing-tools.md.")]
    public async Task<string> GenerateHarFile(
        string outputPath,
        string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            // Ensure output directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Generate timestamp if not provided in filename
            if (!Path.HasExtension(outputPath))
            {
                outputPath = Path.Combine(outputPath, $"network_trace_{DateTime.Now:yyyyMMdd_HHmmss}.har");
            }
            else if (Path.GetExtension(outputPath) != ".har")
            {
                outputPath = Path.ChangeExtension(outputPath, ".har");
            }

            // Start tracing network activity (we'll create a simplified HAR-like format)
            var networkEvents = new List<object>();
            var startTime = DateTime.UtcNow;

            // Enable request/response interception to capture network data
            await session.Page.RouteAsync("**/*", async route =>
            {
                var request = route.Request;
                var requestTime = DateTime.UtcNow;
                
                try
                {
                    // Continue with the request
                    await route.ContinueAsync();
                }
                catch (Exception)
                {
                    // If continuing fails, fulfill with error
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = 500,
                        Body = "Network capture error"
                    });
                }
            });

            // Get current network activity using CDP (Chrome DevTools Protocol)
            var cdpSession = await session.Page.Context.NewCDPSessionAsync(session.Page);
            
            // Enable Network domain
            await cdpSession.SendAsync("Network.enable");
            
            // Collect network events for a short period to demonstrate
            await Task.Delay(2000); // Wait 2 seconds to capture some network activity
            
            // Get network activity using JavaScript evaluation
            var networkData = await session.Page.EvaluateAsync<object>("""

                                                                                       (() => {
                                                                                           const perfEntries = performance.getEntriesByType('navigation')
                                                                                               .concat(performance.getEntriesByType('resource'));
                                                                                           
                                                                                           return perfEntries.map(entry => ({
                                                                                               name: entry.name,
                                                                                               startTime: entry.startTime,
                                                                                               duration: entry.duration,
                                                                                               transferSize: entry.transferSize || 0,
                                                                                               encodedBodySize: entry.encodedBodySize || 0,
                                                                                               decodedBodySize: entry.decodedBodySize || 0,
                                                                                               initiatorType: entry.initiatorType || 'unknown',
                                                                                               nextHopProtocol: entry.nextHopProtocol || 'unknown',
                                                                                               responseStart: entry.responseStart || 0,
                                                                                               responseEnd: entry.responseEnd || 0,
                                                                                               domainLookupStart: entry.domainLookupStart || 0,
                                                                                               domainLookupEnd: entry.domainLookupEnd || 0,
                                                                                               connectStart: entry.connectStart || 0,
                                                                                               connectEnd: entry.connectEnd || 0,
                                                                                               secureConnectionStart: entry.secureConnectionStart || 0,
                                                                                               requestStart: entry.requestStart || 0
                                                                                           }));
                                                                                       })()
                                                                                   
                                                                       """);

            // Create HAR-like structure
            var harData = new
            {
                log = new
                {
                    version = "1.2",
                    creator = new
                    {
                        name = "PlaywrightTester",
                        version = "1.0.0"
                    },
                    browser = new
                    {
                        name = "Playwright",
                        version = "1.53.0"
                    },
                    pages = new[]
                    {
                        new
                        {
                            startedDateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                            id = $"page_{sessionId}",
                            title = await session.Page.TitleAsync(),
                            pageTimings = new
                            {
                                onContentLoad = -1,
                                onLoad = -1
                            }
                        }
                    },
                    entries = networkData,
                    comment = "Generated by PlaywrightTester NetworkTestingTools"
                }
            };

            // Write HAR file
            var harJson = JsonSerializer.Serialize(harData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            await File.WriteAllTextAsync(outputPath, harJson);

            // Disable network tracking
            await cdpSession.SendAsync("Network.disable");
            await cdpSession.DetachAsync();

            // Remove route handler
            await session.Page.UnrouteAsync("**/*");

            var result = new
            {
                success = true,
                sessionId,
                outputPath,
                filename = Path.GetFileName(outputPath),
                fileSize = new FileInfo(outputPath).Length,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                pageUrl = session.Page.Url,
                entriesCount = networkData is JsonElement element ? element.GetArrayLength() : 0,
                note = "HAR file generated with network performance data. For full HAR functionality with request/response bodies, consider using dedicated HAR recording tools."
            };

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to generate HAR file: {ex.Message}";
        }
    }

    // Helper method for smart selector determination
    private static string DetermineSelector(string selector)
    {
        if (selector.Contains('[') || selector.Contains('.') || selector.Contains('#') || 
            selector.Contains('>') || selector.Contains(' ') || selector.Contains(':'))
        {
            return selector;
        }
        
        if (!string.IsNullOrEmpty(selector) && !selector.Contains('='))
        {
            return $"[data-testid='{selector}']";
        }
        
        return selector;
    }
}