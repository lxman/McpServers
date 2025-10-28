using System.ComponentModel;
using System.Text.Json;
using Microsoft.Playwright;
using ModelContextProtocol.Server;
using Playwright.Core.Models;
using Playwright.Core.Services;
using PlaywrightServerMcp.Common;

namespace PlaywrightServerMcp.Tools;

[McpServerToolType]
public class PlaywrightTools(ToolService toolService, PlaywrightSessionManager sessionManager)
{
    [McpServerTool]
    [Description("Launch a browser and create a new session. Returns session ID. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> LaunchBrowser(
        string browserType = "chrome",
        bool headless = true,
        string sessionId = "default",
        int viewportWidth = 1920,
        int viewportHeight = 1080,
        string? deviceEmulation = null,
        string? userAgent = null,
        string? timezone = null,
        string? locale = null,
        string? colorScheme = null,
        string? reducedMotion = null,
        bool enableGeolocation = false,
        bool enableCamera = false,
        bool enableMicrophone = false,
        string? extraHttpHeaders = null)
    {
        var browserOptions = new BrowserLaunchOptions
        {
            ViewportWidth = viewportWidth,
            ViewportHeight = viewportHeight,
            DeviceEmulation = deviceEmulation,
            UserAgent = userAgent,
            Timezone = timezone,
            Locale = locale,
            ColorScheme = colorScheme,
            ReducedMotion = reducedMotion,
            EnableGeolocation = enableGeolocation,
            EnableCamera = enableCamera,
            EnableMicrophone = enableMicrophone,
            ExtraHttpHeaders = extraHttpHeaders
        };

        string result = await sessionManager.CreateSessionAsync(sessionId, browserType, headless, browserOptions);
        
        // Also store in ToolService for backward compatibility
        PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
        if (session != null)
        {
            if (session.Browser != null) toolService.StoreBrowser(sessionId, session.Browser);
            if (session.Context != null) toolService.StoreBrowserContext(sessionId, session.Context);
            if (session.Page != null) toolService.StorePage(sessionId, session.Page);
        }
        
        return result;
    }

    
    [McpServerTool]
    [Description("Navigate to a URL. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> NavigateToUrl(
        string url,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available. Launch browser first.";

            await session.Page.GotoAsync(url);
            return $"Successfully navigated to {url}";
        }
        catch (Exception ex)
        {
            return $"Navigation failed: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Fill a form field using CSS selector or data-testid. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> FillField(
        string selector,
        string value,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string finalSelector = DetermineSelector(selector);
            await session.Page.Locator(finalSelector).FillAsync(value);
            return $"Field {selector} filled in with value {value}";
        }
        catch (Exception ex)
        {
            return $"Failed to fill field: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Click an element using CSS selector or data-testid. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> ClickElement(
        string selector,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string finalSelector = DetermineSelector(selector);
            await session.Page.Locator(finalSelector).ClickAsync();
            return $"Successfully clicked element {selector}";
        }
        catch (Exception ex)
        {
            return $"Failed to click element: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Execute custom JavaScript on the page. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> ExecuteJavaScript(
        string jsCode,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return result?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            return $"JavaScript execution failed: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Select an option from a dropdown. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> SelectOption(
        string selector,
        string value,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string finalSelector = DetermineSelector(selector);
            await session.Page.Locator(finalSelector).SelectOptionAsync(value);
            return $"Selected option '{value}' in dropdown {selector}";
        }
        catch (Exception ex)
        {
            return $"Failed to select option: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get console logs from a browser session. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> GetConsoleLogs(
        string sessionId = "default",
        string? logType = null,
        int maxLogs = 100)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session == null)
                return $"Session {sessionId} not found. Active sessions: [{string.Join(", ", sessionManager.GetActiveSessionIds())}]";

            List<ConsoleLogEntry> logs = session.ConsoleLogs;
            IEnumerable<ConsoleLogEntry> filteredLogs = logs.AsEnumerable();

            if (!string.IsNullOrEmpty(logType))
            {
                filteredLogs = filteredLogs.Where(log => log.Type.Equals(logType, StringComparison.OrdinalIgnoreCase));
            }

            var result = filteredLogs
                .OrderByDescending(log => log.Timestamp)
                .Take(maxLogs)
                .Select(log => new
                {
                    log.Type,
                    log.Text,
                    Timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Location = string.IsNullOrEmpty(log.Url) ? "" : $"{log.Url}:{log.LineNumber}:{log.ColumnNumber}",
                    log.IsError,
                    log.IsWarning
                })
                .ToList();

            var response = new
            {
                SessionId = sessionId,
                SessionActive = session.IsActive,
                LogsFound = result.Count,
                TotalLogsInSession = logs.Count,
                Logs = result
            };

            return JsonSerializer.Serialize(response, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to get console logs: {ex.Message}\nStack trace: {ex.StackTrace}";
        }
    }

    [McpServerTool]
    [Description("Get network activity from a browser session. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> GetNetworkActivity(
        string sessionId = "default",
        string? urlFilter = null)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session == null)
                return $"Session {sessionId} not found. Active sessions: [{string.Join(", ", sessionManager.GetActiveSessionIds())}]";

            List<NetworkLogEntry> networkLogs = session.NetworkLogs;
            IEnumerable<NetworkLogEntry> filteredLogs = networkLogs.AsEnumerable();

            if (!string.IsNullOrEmpty(urlFilter))
            {
                filteredLogs = filteredLogs.Where(log => log.Url.Contains(urlFilter, StringComparison.OrdinalIgnoreCase));
            }

            var result = filteredLogs
                .OrderByDescending(log => log.Timestamp)
                .Take(100)
                .Select(log => new
                {
                    log.Type,
                    log.Method,
                    log.Url,
                    log.Status,
                    log.StatusText,
                    Timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    log.Headers,
                    log.RequestBody,
                    log.ResponseBody,
                    log.ResourceType,
                    log.Duration,
                    log.IsApiCall,
                    log.IsAuthRelated
                })
                .ToList();

            var response = new
            {
                SessionId = sessionId,
                SessionActive = session.IsActive,
                NetworkLogsFound = result.Count,
                TotalNetworkLogsInSession = networkLogs.Count,
                NetworkActivity = result
            };

            return JsonSerializer.Serialize(response, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to get network activity: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get session debug summary with counts and recent activity. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> GetSessionDebugSummary(
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session == null)
                return $"Session {sessionId} not found. Active sessions: [{string.Join(", ", sessionManager.GetActiveSessionIds())}]";

            List<ConsoleLogEntry> recentConsole = session.ConsoleLogs.OrderByDescending(log => log.Timestamp).Take(5).ToList();
            List<NetworkLogEntry> recentNetwork = session.NetworkLogs.OrderByDescending(log => log.Timestamp).Take(5).ToList();
            
            int errorCount = session.ConsoleLogs.Count(log => log.IsError);
            int warningCount = session.ConsoleLogs.Count(log => log.IsWarning);
            int apiCallCount = session.NetworkLogs.Count(log => log.IsApiCall);
            int authCallCount = session.NetworkLogs.Count(log => log.IsAuthRelated);

            var summary = new
            {
                SessionId = sessionId,
                session.IsActive,
                CreatedAt = session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ConsoleLogs = new
                {
                    Total = session.ConsoleLogs.Count,
                    Errors = errorCount,
                    Warnings = warningCount,
                    Recent = recentConsole.Select(log => new { log.Type, log.Text, Timestamp = log.Timestamp.ToString("HH:mm:ss.fff") }).ToList()
                },
                NetworkLogs = new
                {
                    Total = session.NetworkLogs.Count,
                    ApiCalls = apiCallCount,
                    AuthRelated = authCallCount,
                    Recent = recentNetwork.Select(log => new { log.Type, log.Method, log.Url, log.Status, Timestamp = log.Timestamp.ToString("HH:mm:ss.fff") }).ToList()
                },
                ActiveSessions = sessionManager.GetActiveSessionIds().ToList()
            };

            return JsonSerializer.Serialize(summary, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to get session debug summary: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Clear console and network logs for a browser session. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> ClearSessionLogs(
        string sessionId = "default",
        bool clearConsole = true,
        bool clearNetwork = true)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session == null)
                return $"Session {sessionId} not found.";

            var clearedItems = new List<string>();

            if (clearConsole)
            {
                int consoleCount = session.ConsoleLogs.Count;
                session.ConsoleLogs.Clear();
                clearedItems.Add($"{consoleCount} console logs");
            }

            if (clearNetwork)
            {
                int networkCount = session.NetworkLogs.Count;
                session.NetworkLogs.Clear();
                clearedItems.Add($"{networkCount} network logs");
            }

            return $"Successfully cleared {string.Join(" and ", clearedItems)} for session {sessionId}.";
        }
        catch (Exception ex)
        {
            return $"Failed to clear session logs: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Close browser session and cleanup resources. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> CloseBrowser(
        string sessionId = "default")
    {
        try
        {
            bool success = await sessionManager.CloseSessionAsync(sessionId);
            if (success)
            {
                return $"Browser session {sessionId} closed successfully";
            }

            return $"Session {sessionId} not found or already closed";
        }
        catch (Exception ex)
        {
            return $"Failed to close browser: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Extract comprehensive CSS style information for a specific element. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> InspectElementStyles(
        string selector,
        string sessionId = "default",
        bool includeAllStyles = false)
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string finalSelector = DetermineSelector(selector);
            
            var jsCode = $$"""

                                           (() => {
                                               const element = document.querySelector('{{finalSelector.Replace("'", "\\'")}}');
                                               if (!element) {
                                                   return { error: 'Element not found' };
                                               }
                                               
                                               const computedStyles = window.getComputedStyle(element);
                                               const rect = element.getBoundingClientRect();
                                               
                                               const result = {
                                                   selector: '{{finalSelector.Replace("'", "\\'")}}',
                                                   tagName: element.tagName.toLowerCase(),
                                                   dimensions: {
                                                       width: rect.width,
                                                       height: rect.height,
                                                       top: rect.top,
                                                       left: rect.left,
                                                       right: rect.right,
                                                       bottom: rect.bottom
                                                   },
                                                   colors: {
                                                       color: computedStyles.color,
                                                       backgroundColor: computedStyles.backgroundColor,
                                                       borderColor: computedStyles.borderColor,
                                                       borderTopColor: computedStyles.borderTopColor,
                                                       borderRightColor: computedStyles.borderRightColor,
                                                       borderBottomColor: computedStyles.borderBottomColor,
                                                       borderLeftColor: computedStyles.borderLeftColor
                                                   },
                                                   borders: {
                                                       borderWidth: computedStyles.borderWidth,
                                                       borderStyle: computedStyles.borderStyle,
                                                       borderTopWidth: computedStyles.borderTopWidth,
                                                       borderRightWidth: computedStyles.borderRightWidth,
                                                       borderBottomWidth: computedStyles.borderBottomWidth,
                                                       borderLeftWidth: computedStyles.borderLeftWidth,
                                                       borderRadius: computedStyles.borderRadius
                                                   },
                                                   spacing: {
                                                       margin: computedStyles.margin,
                                                       marginTop: computedStyles.marginTop,
                                                       marginRight: computedStyles.marginRight,
                                                       marginBottom: computedStyles.marginBottom,
                                                       marginLeft: computedStyles.marginLeft,
                                                       padding: computedStyles.padding,
                                                       paddingTop: computedStyles.paddingTop,
                                                       paddingRight: computedStyles.paddingRight,
                                                       paddingBottom: computedStyles.paddingBottom,
                                                       paddingLeft: computedStyles.paddingLeft
                                                   },
                                                   typography: {
                                                       fontFamily: computedStyles.fontFamily,
                                                       fontSize: computedStyles.fontSize,
                                                       fontWeight: computedStyles.fontWeight,
                                                       fontStyle: computedStyles.fontStyle,
                                                       lineHeight: computedStyles.lineHeight,
                                                       letterSpacing: computedStyles.letterSpacing,
                                                       textAlign: computedStyles.textAlign,
                                                       textDecoration: computedStyles.textDecoration,
                                                       textTransform: computedStyles.textTransform
                                                   },
                                                   layout: {
                                                       display: computedStyles.display,
                                                       position: computedStyles.position,
                                                       flexDirection: computedStyles.flexDirection,
                                                       flexWrap: computedStyles.flexWrap,
                                                       justifyContent: computedStyles.justifyContent,
                                                       alignItems: computedStyles.alignItems,
                                                       gridTemplateColumns: computedStyles.gridTemplateColumns,
                                                       gridTemplateRows: computedStyles.gridTemplateRows,
                                                       zIndex: computedStyles.zIndex,
                                                       overflow: computedStyles.overflow
                                                   },
                                                   visibility: {
                                                       visibility: computedStyles.visibility,
                                                       opacity: computedStyles.opacity,
                                                       transform: computedStyles.transform
                                                   },
                                                   textContent: element.textContent?.trim().substring(0, 100) || '',
                                                   classes: Array.from(element.classList),
                                                   attributes: {}
                                               };
                                               
                                               // Capture key attributes
                                               ['id', 'data-testid', 'role', 'aria-label', 'title', 'alt'].forEach(attr => {
                                                   if (element.hasAttribute(attr)) {
                                                       result.attributes[attr] = element.getAttribute(attr);
                                                   }
                                               });
                                               
                                               {{(includeAllStyles ? """

                                                                                         // Include all computed styles if requested
                                                                                         result.allComputedStyles = {};
                                                                                         for (let prop of computedStyles) {
                                                                                             result.allComputedStyles[prop] = computedStyles.getPropertyValue(prop);
                                                                                         }
                                                                                         
                                                                     """ : "")}}
                                               
                                               return result;
                                           })()
                                       
                           """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to inspect element styles: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get detailed color analysis for elements (useful for design system validation). See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> AnalyzePageColors(
        string? containerSelector = null,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string container = string.IsNullOrEmpty(containerSelector) ? "document.body" : $"document.querySelector('{containerSelector.Replace("'", "\\'")}')";
            
            var jsCode = $$$"""

                                            (() => {
                                                const container = {{{container}}};
                                                if (!container) {
                                                    return { error: 'Container not found' };
                                                }
                                                
                                                const allElements = container.querySelectorAll('*');
                                                const colorMap = new Map();
                                                const colorUsage = [];
                                                
                                                allElements.forEach((element, index) => {
                                                    const computedStyles = window.getComputedStyle(element);
                                                    const rect = element.getBoundingClientRect();
                                                    
                                                    // Skip elements that are not visible
                                                    if (rect.width === 0 || rect.height === 0) return;
                                                    
                                                    const elementInfo = {
                                                        selector: element.tagName.toLowerCase() + (element.id ? '#' + element.id : '') + 
                                                                 (element.className ? '.' + Array.from(element.classList).join('.') : ''),
                                                        tagName: element.tagName.toLowerCase(),
                                                        colors: {
                                                            color: computedStyles.color,
                                                            backgroundColor: computedStyles.backgroundColor,
                                                            borderColor: computedStyles.borderColor
                                                        },
                                                        textContent: element.textContent?.trim().substring(0, 50) || '',
                                                        isVisible: computedStyles.visibility === 'visible' && computedStyles.opacity !== '0'
                                                    };
                                                    
                                                    // Track unique colors
                                                    [elementInfo.colors.color, elementInfo.colors.backgroundColor, elementInfo.colors.borderColor]
                                                        .forEach(color => {
                                                            if (color && color !== 'rgba(0, 0, 0, 0)' && color !== 'transparent') {
                                                                const normalizedColor = color;
                                                                if (!colorMap.has(normalizedColor)) {
                                                                    colorMap.set(normalizedColor, []);
                                                                }
                                                                colorMap.get(normalizedColor).push({
                                                                    element: elementInfo.selector,
                                                                    property: color === elementInfo.colors.color ? 'color' : 
                                                                             color === elementInfo.colors.backgroundColor ? 'backgroundColor' : 'borderColor'
                                                                });
                                                            }
                                                        });
                                                    
                                                    if (elementInfo.isVisible) {
                                                        colorUsage.push(elementInfo);
                                                    }
                                                });
                                                
                                                // Convert Map to object for JSON serialization
                                                const colorInventory = {};
                                                colorMap.forEach((usage, color) => {
                                                    colorInventory[color] = {
                                                        usageCount: usage.length,
                                                        usedBy: usage.slice(0, 10) // Limit to first 10 instances
                                                    };
                                                });
                                                
                                                return {
                                                    colorInventory: colorInventory,
                                                    totalUniqueColors: colorMap.size,
                                                    visibleElements: colorUsage.length,
                                                    analysis: {
                                                        mostUsedColors: Object.entries(colorInventory)
                                                            .sort((a, b) => b[1].usageCount - a[1].usageCount)
                                                            .slice(0, 10)
                                                            .map(([color, data]) => ({{ color, usageCount: data.usageCount }}))
                                                    }
                                                };
                                            })()
                                        
                            """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to analyze page colors: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Compare visual styles between multiple elements. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> CompareElementStyles(
        string selectorsJson,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string[]? selectors = JsonSerializer.Deserialize<string[]>(selectorsJson);
            if (selectors == null || selectors.Length == 0)
                return "Invalid selectors array provided";

            string[] finalSelectors = selectors.Select(DetermineSelector).ToArray();
            string selectorsJsArray = JsonSerializer.Serialize(finalSelectors);
            
            var jsCode = $$"""

                                           const selectors = {{selectorsJsArray}};
                                           const results = [];
                                           const comparisons = [];
                                           
                                           // Get styles for each element
                                           selectors.forEach((selector, index) => {
                                               const element = document.querySelector(selector);
                                               if (!element) {
                                                   results.push({ 
                                                       selector: selector, 
                                                       error: 'Element not found' 
                                                   });
                                                   return;
                                               }
                                               
                                               const computedStyles = window.getComputedStyle(element);
                                               const rect = element.getBoundingClientRect();
                                               
                                               results.push({
                                                   selector: selector,
                                                   tagName: element.tagName.toLowerCase(),
                                                   styles: {
                                                       color: computedStyles.color,
                                                       backgroundColor: computedStyles.backgroundColor,
                                                       fontSize: computedStyles.fontSize,
                                                       fontWeight: computedStyles.fontWeight,
                                                       fontFamily: computedStyles.fontFamily,
                                                       borderColor: computedStyles.borderColor,
                                                       borderWidth: computedStyles.borderWidth,
                                                       borderStyle: computedStyles.borderStyle,
                                                       margin: computedStyles.margin,
                                                       padding: computedStyles.padding,
                                                       display: computedStyles.display,
                                                       position: computedStyles.position
                                                   },
                                                   dimensions: {
                                                       width: rect.width,
                                                       height: rect.height
                                                   },
                                                   textContent: element.textContent?.trim().substring(0, 50) || ''
                                               });
                                           });
                                           
                                           // Compare styles between elements
                                           for (let i = 0; i < results.length; i++) {
                                               for (let j = i + 1; j < results.length; j++) {
                                                   const a = results[i];
                                                   const b = results[j];
                                                   
                                                   if (a.error || b.error) continue;
                                                   
                                                   const differences = [];
                                                   const similarities = [];
                                                   
                                                   Object.keys(a.styles).forEach(property => {
                                                       if (a.styles[property] === b.styles[property]) {
                                                           similarities.push(property);
                                                       } else {
                                                           differences.push({
                                                               property: property,
                                                               elementA: a.styles[property],
                                                               elementB: b.styles[property]
                                                           });
                                                       }
                                                   });
                                                   
                                                   comparisons.push({
                                                       elementA: a.selector,
                                                       elementB: b.selector,
                                                       differences: differences,
                                                       similarities: similarities,
                                                       similarityPercentage: Math.round((similarities.length / Object.keys(a.styles).length) * 100)
                                                   });
                                               }
                                           }
                                           
                                           return {
                                               elements: results,
                                               comparisons: comparisons,
                                               summary: {
                                                   totalElements: results.filter(r => !r.error).length,
                                                   totalComparisons: comparisons.length,
                                                   averageSimilarity: comparisons.length > 0 ? 
                                                       Math.round(comparisons.reduce((sum, comp) => sum + comp.similarityPercentage, 0) / comparisons.length) : 0
                                               }
                                           };
                                       
                           """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to compare element styles: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Extract design system information and CSS custom properties. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> ExtractDesignTokens(
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";
            
            var jsCode = """

                                         // Extract CSS custom properties (CSS variables)
                                         const allStyleSheets = Array.from(document.styleSheets);
                                         const customProperties = new Map();
                                         const classDefinitions = new Map();
                                         
                                         // Extract from computed styles on document root
                                         const rootStyles = window.getComputedStyle(document.documentElement);
                                         for (let prop of rootStyles) {
                                             if (prop.startsWith('--')) {
                                                 customProperties.set(prop, rootStyles.getPropertyValue(prop).trim());
                                             }
                                         }
                                         
                                         // Try to extract from stylesheets (if accessible)
                                         allStyleSheets.forEach(sheet => {
                                             try {
                                                 if (sheet.cssRules) {
                                                     Array.from(sheet.cssRules).forEach(rule => {
                                                         if (rule.type === CSSRule.STYLE_RULE) {
                                                             // Collect class-based styles
                                                             if (rule.selectorText && rule.selectorText.includes('.')) {
                                                                 const className = rule.selectorText.replace(/[^.a-zA-Z0-9_-]/g, '');
                                                                 if (className) {
                                                                     classDefinitions.set(className, rule.cssText);
                                                                 }
                                                             }
                                                         }
                                                     });
                                                 }
                                             } catch (e) {
                                                 // Cross-origin stylesheet access might be blocked
                                             }
                                         });
                                         
                                         // Analyze common design patterns
                                         const commonElements = document.querySelectorAll('button, .btn, .button, h1, h2, h3, h4, h5, h6, .card, .container, .row, .col');
                                         const designPatterns = [];
                                         
                                         commonElements.forEach(element => {
                                             const computedStyles = window.getComputedStyle(element);
                                             designPatterns.push({
                                                 element: element.tagName.toLowerCase() + (element.className ? '.' + Array.from(element.classList).join('.') : ''),
                                                 styles: {
                                                     fontSize: computedStyles.fontSize,
                                                     fontWeight: computedStyles.fontWeight,
                                                     color: computedStyles.color,
                                                     backgroundColor: computedStyles.backgroundColor,
                                                     borderRadius: computedStyles.borderRadius,
                                                     padding: computedStyles.padding,
                                                     margin: computedStyles.margin
                                                 }
                                             });
                                         });
                                         
                                         // Convert Maps to objects for JSON serialization
                                         const customPropsObj = {};
                                         customProperties.forEach((value, key) => {
                                             customPropsObj[key] = value;
                                         });
                                         
                                         const classDefsObj = {};
                                         classDefinitions.forEach((value, key) => {
                                             classDefsObj[key] = value;
                                         });
                                         
                                         return {
                                             customProperties: customPropsObj,
                                             classDefinitions: classDefsObj,
                                             designPatterns: designPatterns,
                                             summary: {
                                                 totalCustomProperties: customProperties.size,
                                                 totalClassDefinitions: classDefinitions.size,
                                                 totalAnalyzedElements: designPatterns.length
                                             }
                                         };
                                     
                         """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to extract design tokens: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get layout analysis for responsive design testing. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> AnalyzeLayout(
        string? containerSelector = null,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string container = string.IsNullOrEmpty(containerSelector) ? "document.body" : $"document.querySelector('{containerSelector.Replace("'", "\\'")}')";
            
            var jsCode = $$"""

                                           const container = {{container}};
                                           if (!container) {
                                               return { error: 'Container not found' };
                                           }
                                           
                                           const viewport = {
                                               width: window.innerWidth,
                                               height: window.innerHeight
                                           };
                                           
                                           const containerRect = container.getBoundingClientRect();
                                           const elements = container.querySelectorAll('*');
                                           
                                           const layoutInfo = [];
                                           const flexContainers = [];
                                           const gridContainers = [];
                                           const overlapping = [];
                                           
                                           elements.forEach(element => {
                                               const computedStyles = window.getComputedStyle(element);
                                               const rect = element.getBoundingClientRect();
                                               
                                               // Skip elements that are not visible
                                               if (rect.width === 0 || rect.height === 0) return;
                                               
                                               const elementInfo = {
                                                   selector: element.tagName.toLowerCase() + (element.id ? '#' + element.id : '') + 
                                                            (element.className ? '.' + Array.from(element.classList).slice(0, 3).join('.') : ''),
                                                   tagName: element.tagName.toLowerCase(),
                                                   position: {
                                                       x: rect.left,
                                                       y: rect.top,
                                                       width: rect.width,
                                                       height: rect.height
                                                   },
                                                   styles: {
                                                       display: computedStyles.display,
                                                       position: computedStyles.position,
                                                       flexDirection: computedStyles.flexDirection,
                                                       justifyContent: computedStyles.justifyContent,
                                                       alignItems: computedStyles.alignItems,
                                                       gridTemplateColumns: computedStyles.gridTemplateColumns,
                                                       gridTemplateRows: computedStyles.gridTemplateRows,
                                                       overflow: computedStyles.overflow,
                                                       zIndex: computedStyles.zIndex
                                                   },
                                                   responsiveIndicators: {
                                                       hasMediaQueries: false, // Would need stylesheet analysis
                                                       usesViewportUnits: [computedStyles.width, computedStyles.height, computedStyles.fontSize]
                                                           .some(val => val && (val.includes('vw') || val.includes('vh') || val.includes('vmin') || val.includes('vmax'))),
                                                       usesFlexbox: computedStyles.display === 'flex',
                                                       usesGrid: computedStyles.display === 'grid'
                                                   }
                                               };
                                               
                                               layoutInfo.push(elementInfo);
                                               
                                               // Collect flex containers
                                               if (computedStyles.display === 'flex') {
                                                   flexContainers.push(elementInfo);
                                               }
                                               
                                               // Collect grid containers
                                               if (computedStyles.display === 'grid') {
                                                   gridContainers.push(elementInfo);
                                               }
                                           });
                                           
                                           // Check for overlapping elements
                                           for (let i = 0; i < layoutInfo.length; i++) {
                                               for (let j = i + 1; j < layoutInfo.length; j++) {
                                                   const a = layoutInfo[i];
                                                   const b = layoutInfo[j];
                                                   
                                                   // Simple overlap detection
                                                   if (a.position.x < b.position.x + b.position.width &&
                                                       a.position.x + a.position.width > b.position.x &&
                                                       a.position.y < b.position.y + b.position.height &&
                                                       a.position.y + a.position.height > b.position.y) {
                                                       overlapping.push({
                                                           elementA: a.selector,
                                                           elementB: b.selector,
                                                           overlap: 'detected'
                                                       });
                                                   }
                                               }
                                           }
                                           
                                           return {
                                               viewport: viewport,
                                               containerInfo: {
                                                   width: containerRect.width,
                                                   height: containerRect.height,
                                                   elementCount: layoutInfo.length
                                               },
                                               flexContainers: flexContainers,
                                               gridContainers: gridContainers,
                                               overlappingElements: overlapping.slice(0, 20), // Limit output
                                               summary: {
                                                   totalElements: layoutInfo.length,
                                                   flexContainerCount: flexContainers.length,
                                                   gridContainerCount: gridContainers.length,
                                                   overlappingCount: overlapping.length,
                                                   responsiveElementCount: layoutInfo.filter(el => 
                                                       el.responsiveIndicators.usesViewportUnits || 
                                                       el.responsiveIndicators.usesFlexbox || 
                                                       el.responsiveIndicators.usesGrid
                                                   ).length
                                               }
                                           };
                                       
                           """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to analyze layout: {ex.Message}";
        }
    }
    
    [McpServerTool]
    [Description("Launch a browser with mobile device emulation. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> LaunchMobileBrowser(
        string deviceType,
        string browserType = "chrome",
        bool headless = true,
        string sessionId = "default")
    {
        return await LaunchBrowser(
            browserType: browserType,
            headless: headless,
            sessionId: sessionId,
            deviceEmulation: deviceType
        );
    }

    [McpServerTool]
    [Description("Launch a browser with dark mode enabled. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> LaunchDarkModeBrowser(
        string browserType = "chrome",
        bool headless = true,
        string sessionId = "default",
        int viewportWidth = 1920,
        int viewportHeight = 1080)
    {
        return await LaunchBrowser(
            browserType: browserType,
            headless: headless,
            sessionId: sessionId,
            viewportWidth: viewportWidth,
            viewportHeight: viewportHeight,
            colorScheme: "dark",
            reducedMotion: "reduce"
        );
    }

    [McpServerTool]
    [Description("Launch a browser with accessibility testing configuration. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> LaunchAccessibilityBrowser(
        string browserType = "chrome",
        bool headless = true,
        string sessionId = "default",
        int viewportWidth = 1920,
        int viewportHeight = 1080)
    {
        return await LaunchBrowser(
            browserType: browserType,
            headless: headless,
            sessionId: sessionId,
            viewportWidth: viewportWidth,
            viewportHeight: viewportHeight,
            colorScheme: "dark",
            reducedMotion: "reduce",
            extraHttpHeaders: "{\"Accept-Language\": \"en-US,en;q=0.9\"}"
        );
    }

    [McpServerTool]
    [Description("Get current browser viewport information. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> GetViewportInfo(
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = """

                                         return {
                                             viewportWidth: window.innerWidth,
                                             viewportHeight: window.innerHeight,
                                             screenWidth: window.screen.width,
                                             screenHeight: window.screen.height,
                                             devicePixelRatio: window.devicePixelRatio,
                                             colorScheme: window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light',
                                             reducedMotion: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'reduce' : 'no-preference',
                                             touchSupport: 'ontouchstart' in window || navigator.maxTouchPoints > 0,
                                             userAgent: navigator.userAgent,
                                             platform: navigator.platform,
                                             language: navigator.language,
                                             timezone: Intl.DateTimeFormat().resolvedOptions().timeZone
                                         };
                                     
                         """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return $"Failed to get viewport info: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Change viewport size of existing browser session. See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> SetViewportSize(
        int width,
        int height,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            await session.Page.SetViewportSizeAsync(width, height);
            return $"Viewport size changed to {width}x{height} for session {sessionId}";
        }
        catch (Exception ex)
        {
            return $"Failed to set viewport size: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Simulate device orientation change (portrait/landscape). See skills/playwright-mcp/tools/playwright-tools.md.")]
    public async Task<string> RotateDevice(
        string orientation,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            // Get current viewport size
            PageViewportSizeResult? currentViewport = session.Page.ViewportSize;
            if (currentViewport == null)
                return "Unable to get current viewport size";

            int newWidth, newHeight;
            if (orientation.ToLower() == "landscape")
            {
                // Landscape: width > height
                newWidth = Math.Max(currentViewport.Width, currentViewport.Height);
                newHeight = Math.Min(currentViewport.Width, currentViewport.Height);
            }
            else
            {
                // Portrait: height > width
                newWidth = Math.Min(currentViewport.Width, currentViewport.Height);
                newHeight = Math.Max(currentViewport.Width, currentViewport.Height);
            }

            await session.Page.SetViewportSizeAsync(newWidth, newHeight);
            return $"Device rotated to {orientation} orientation ({newWidth}x{newHeight}) for session {sessionId}";
        }
        catch (Exception ex)
        {
            return $"Failed to rotate device: {ex.Message}";
        }
    }

    // Helper method for smart selector determination
    private static string DetermineSelector(string selector)
    {
        // FIXED: Smart selector determination instead of automatic wrapping
        
        // If it's already a CSS selector (contains CSS syntax), use as-is
        if (selector.Contains('[') || selector.Contains('.') || selector.Contains('#') || 
            selector.Contains('>') || selector.Contains(' ') || selector.Contains(':'))
        {
            return selector;
        }
        
        // If it looks like a simple data-testid value, wrap it
        // This preserves the data-testid functionality while avoiding the wrapping bug
        if (!string.IsNullOrEmpty(selector) && !selector.Contains('='))
        {
            return $"[data-testid='{selector}']";
        }
        
        // Default: use as-is
        return selector;
    }
}
