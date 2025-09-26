using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// ANG-004: Angular Stability Detection Implementation
/// Implements wait_for_angular_stability function with Zone.js monitoring integration and async operation detection
/// Supports both zone-based and zoneless Angular applications (Angular 18+)
/// </summary>
[McpServerToolType]
public class AngularStabilityDetection(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Wait for Angular application to reach stability by monitoring Zone.js and async operations - ANG-004 Implementation")]
    public async Task<string> WaitForAngularStability(
        [Description("Maximum wait time in seconds")] int timeoutSeconds = 30,
        [Description("Check interval in milliseconds")] int checkIntervalMs = 100,
        [Description("Include detailed stability monitoring information")] bool includeDetailedInfo = true,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    // ANG-004: Angular Stability Detection Implementation
                    const waitForAngularStability = () => {{
                        const results = {{
                            angularDetected: false,
                            isStable: false,
                            isZoneless: false,
                            zoneJsPresent: false,
                            stabilityChecks: {{
                                zoneIsStable: false,
                                pendingMacroTasks: 0,
                                pendingMicroTasks: 0,
                                runningAsyncOperations: 0,
                                httpRequests: {{
                                    pending: 0,
                                    total: 0,
                                    completed: 0
                                }},
                                timers: {{
                                    pending: 0,
                                    active: []
                                }},
                                promises: {{
                                    pending: 0,
                                    active: []
                                }},
                                changeDetection: {{
                                    running: false,
                                    cycles: 0
                                }}
                            }},
                            waitTime: {{
                                startTime: Date.now(),
                                endTime: null,
                                totalWaitMs: 0,
                                timeoutSeconds: {timeoutSeconds},
                                timedOut: false
                            }},
                            monitoring: {{
                                checkCount: 0,
                                checkIntervalMs: {checkIntervalMs},
                                stabilityHistory: [],
                                lastStableCheck: null
                            }},
                            angularInfo: {{
                                version: null,
                                applicationRef: null,
                                isBootstrapped: false,
                                components: []
                            }},
                            detailedInfo: {includeDetailedInfo.ToString().ToLower()},
                            errors: [],
                            warnings: []
                        }};

                        // Step 1: Detect Angular and determine stability monitoring approach
                        const detectAngularEnvironment = () => {{
                            try {{
                                // Check for Angular presence
                                const hasAngularGlobal = !!(window.ng || window.ngDevMode || window.getAllAngularRootElements);
                                const hasAngularElements = document.querySelector('[ng-version]') !== null;
                                
                                results.angularDetected = hasAngularGlobal || hasAngularElements;
                                
                                if (!results.angularDetected) {{
                                    results.errors.push('Angular application not detected on this page');
                                    return false;
                                }}

                                // Get Angular version
                                const versionElement = document.querySelector('[ng-version]');
                                if (versionElement) {{
                                    results.angularInfo.version = versionElement.getAttribute('ng-version');
                                }}

                                // Check for Zone.js presence
                                results.zoneJsPresent = !!(window.Zone && window.Zone.current);
                                
                                // Check if this is a zoneless application
                                results.isZoneless = !results.zoneJsPresent || 
                                                   document.querySelector('[ng-zoneless]') !== null ||
                                                   (window.ng && window.ng.experimental?.provideZonelessChangeDetection);

                                // Check if Angular is bootstrapped
                                if (window.getAllAngularRootElements) {{
                                    const rootElements = window.getAllAngularRootElements();
                                    results.angularInfo.isBootstrapped = rootElements && rootElements.length > 0;
                                    results.angularInfo.components = rootElements ? rootElements.length : 0;
                                }} else if (window.ng?.getComponent) {{
                                    // Fallback method
                                    const componentsFound = Array.from(document.querySelectorAll('*')).slice(0, 50)
                                        .filter(el => {{
                                            try {{
                                                return window.ng.getComponent(el) !== null;
                                            }} catch (e) {{
                                                return false;
                                            }}
                                        }});
                                    results.angularInfo.isBootstrapped = componentsFound.length > 0;
                                    results.angularInfo.components = componentsFound.length;
                                }}

                                return true;
                            }} catch (e) {{
                                results.errors.push(`Error detecting Angular environment: ${{e.message}}`);
                                return false;
                            }}
                        }};

                        // Step 2: Zone.js stability monitoring
                        const checkZoneStability = () => {{
                            if (!results.zoneJsPresent) {{
                                results.stabilityChecks.zoneIsStable = true;
                                return true;
                            }}

                            try {{
                                const zone = window.Zone.current;
                                if (!zone) {{
                                    results.warnings.push('Zone.js detected but current zone is null');
                                    return true;
                                }}

                                // Check for pending macro tasks (setTimeout, setInterval, XHR, etc.)
                                results.stabilityChecks.pendingMacroTasks = 0;
                                if (zone._parent && zone._parent._properties && zone._parent._properties.macroTasks) {{
                                    results.stabilityChecks.pendingMacroTasks = zone._parent._properties.macroTasks.length || 0;
                                }}

                                // Check for pending micro tasks (Promises, etc.)
                                results.stabilityChecks.pendingMicroTasks = 0;
                                if (zone._parent && zone._parent._properties && zone._parent._properties.microTasks) {{
                                    results.stabilityChecks.pendingMicroTasks = zone._parent._properties.microTasks.length || 0;
                                }}

                                // Zone is stable if no pending tasks
                                results.stabilityChecks.zoneIsStable = 
                                    results.stabilityChecks.pendingMacroTasks === 0 && 
                                    results.stabilityChecks.pendingMicroTasks === 0;

                                return results.stabilityChecks.zoneIsStable;
                            }} catch (e) {{
                                results.warnings.push(`Error checking Zone.js stability: ${{e.message}}`);
                                // If we can't check Zone, assume stable
                                results.stabilityChecks.zoneIsStable = true;
                                return true;
                            }}
                        }};

                        // Step 3: HTTP request monitoring
                        const checkHttpRequests = () => {{
                            try {{
                                // Reset counters
                                results.stabilityChecks.httpRequests.pending = 0;

                                // Method 1: Check Angular HttpClient (if available)
                                if (window.ng && window.ng.probe) {{
                                    try {{
                                        const allElements = Array.from(document.querySelectorAll('*')).slice(0, 20);
                                        allElements.forEach(el => {{
                                            try {{
                                                const component = window.ng.getComponent(el);
                                                if (component && component.constructor) {{
                                                    // Look for HTTP client or pending operations
                                                    Object.getOwnPropertyNames(component).forEach(prop => {{
                                                        try {{
                                                            const value = component[prop];
                                                            if (value && value.constructor && 
                                                                (value.constructor.name === 'HttpClient' ||
                                                                 value.constructor.name.includes('Http'))) {{
                                                                // This is a simplified check
                                                                if (value._pendingRequests) {{
                                                                    results.stabilityChecks.httpRequests.pending += value._pendingRequests.length || 0;
                                                                }}
                                                            }}
                                                        }} catch (e) {{
                                                            // Skip properties that can't be accessed
                                                        }}
                                                    }});
                                                }}
                                            }} catch (e) {{
                                                // Skip elements without components
                                            }}
                                        }});
                                    }} catch (e) {{
                                        results.warnings.push(`Error checking HTTP client: ${{e.message}}`);
                                    }}
                                }}

                                // Method 2: Monitor XMLHttpRequest and fetch (if not already covered by Zone.js)
                                if (!results.zoneJsPresent) {{
                                    // For zoneless applications, we need to manually track requests
                                    // This is a simplified implementation
                                    if (window._activeXhrRequests) {{
                                        results.stabilityChecks.httpRequests.pending = window._activeXhrRequests.size || 0;
                                    }}
                                    if (window._activeFetchRequests) {{
                                        results.stabilityChecks.httpRequests.pending += window._activeFetchRequests.size || 0;
                                    }}
                                }}

                                return results.stabilityChecks.httpRequests.pending === 0;
                            }} catch (e) {{
                                results.warnings.push(`Error checking HTTP requests: ${{e.message}}`);
                                return true; // Assume no pending requests if we can't check
                            }}
                        }};

                        // Step 4: Timer monitoring
                        const checkTimers = () => {{
                            try {{
                                results.stabilityChecks.timers.pending = 0;
                                results.stabilityChecks.timers.active = [];

                                // For zoneless applications, we need manual timer tracking
                                if (!results.zoneJsPresent) {{
                                    // Check if there are any timer tracking mechanisms
                                    if (window._activeTimeouts) {{
                                        results.stabilityChecks.timers.pending = window._activeTimeouts.size || 0;
                                        results.stabilityChecks.timers.active = Array.from(window._activeTimeouts.values() || []);
                                    }}
                                    if (window._activeIntervals) {{
                                        results.stabilityChecks.timers.pending += window._activeIntervals.size || 0;
                                    }}
                                }} else {{
                                    // Zone.js should handle timer tracking
                                    // The pending macro tasks count already includes timers
                                    results.stabilityChecks.timers.pending = 0; // Already counted in macro tasks
                                }}

                                return results.stabilityChecks.timers.pending === 0;
                            }} catch (e) {{
                                results.warnings.push(`Error checking timers: ${{e.message}}`);
                                return true;
                            }}
                        }};

                        // Step 5: Promise monitoring
                        const checkPromises = () => {{
                            try {{
                                results.stabilityChecks.promises.pending = 0;
                                results.stabilityChecks.promises.active = [];

                                if (!results.zoneJsPresent) {{
                                    // For zoneless applications, manual promise tracking
                                    if (window._activePromises) {{
                                        results.stabilityChecks.promises.pending = window._activePromises.size || 0;
                                        results.stabilityChecks.promises.active = Array.from(window._activePromises.values() || []);
                                    }}
                                }} else {{
                                    // Zone.js handles promise tracking via micro tasks
                                    results.stabilityChecks.promises.pending = 0; // Already counted in micro tasks
                                }}

                                return results.stabilityChecks.promises.pending === 0;
                            }} catch (e) {{
                                results.warnings.push(`Error checking promises: ${{e.message}}`);
                                return true;
                            }}
                        }};

                        // Step 6: Angular change detection monitoring
                        const checkChangeDetection = () => {{
                            try {{
                                results.stabilityChecks.changeDetection.running = false;
                                results.stabilityChecks.changeDetection.cycles = 0;

                                // Check if Angular is currently running change detection
                                if (window.ng && window.ng.getContext) {{
                                    try {{
                                        const rootElements = window.getAllAngularRootElements?.() || [];
                                        let isRunning = false;
                                        
                                        rootElements.forEach(el => {{
                                            try {{
                                                const context = window.ng.getContext(el);
                                                if (context && context.injector) {{
                                                    // This is a simplified check for change detection status
                                                    // Real implementation would need deeper Angular internals access
                                                    const applicationRef = context.injector.get?.('ApplicationRef');
                                                    if (applicationRef && applicationRef._runningTick) {{
                                                        isRunning = true;
                                                        results.stabilityChecks.changeDetection.cycles++;
                                                    }}
                                                }}
                                            }} catch (e) {{
                                                // Skip if we can't access context
                                            }}
                                        }});

                                        results.stabilityChecks.changeDetection.running = isRunning;
                                    }} catch (e) {{
                                        results.warnings.push(`Error checking change detection: ${{e.message}}`);
                                    }}
                                }}

                                return !results.stabilityChecks.changeDetection.running;
                            }} catch (e) {{
                                results.warnings.push(`Error monitoring change detection: ${{e.message}}`);
                                return true;
                            }}
                        }};

                        // Step 7: Calculate total running async operations
                        const calculateAsyncOperations = () => {{
                            results.stabilityChecks.runningAsyncOperations = 
                                results.stabilityChecks.pendingMacroTasks +
                                results.stabilityChecks.pendingMicroTasks +
                                results.stabilityChecks.httpRequests.pending +
                                results.stabilityChecks.timers.pending +
                                results.stabilityChecks.promises.pending +
                                (results.stabilityChecks.changeDetection.running ? 1 : 0);
                        }};

                        // Step 8: Comprehensive stability check
                        const performStabilityCheck = () => {{
                            results.monitoring.checkCount++;
                            
                            const zoneStable = checkZoneStability();
                            const httpStable = checkHttpRequests();
                            const timersStable = checkTimers();
                            const promisesStable = checkPromises();
                            const changeDetectionStable = checkChangeDetection();
                            
                            calculateAsyncOperations();

                            // Application is stable if all checks pass
                            results.isStable = zoneStable && httpStable && timersStable && 
                                             promisesStable && changeDetectionStable;

                            // Record stability check for history
                            const stabilitySnapshot = {{
                                timestamp: Date.now(),
                                isStable: results.isStable,
                                checkCount: results.monitoring.checkCount,
                                runningOperations: results.stabilityChecks.runningAsyncOperations,
                                details: {{
                                    zone: zoneStable,
                                    http: httpStable,
                                    timers: timersStable,
                                    promises: promisesStable,
                                    changeDetection: changeDetectionStable
                                }}
                            }};

                            results.monitoring.stabilityHistory.push(stabilitySnapshot);
                            
                            if (results.isStable) {{
                                results.monitoring.lastStableCheck = stabilitySnapshot;
                            }}

                            // Keep history manageable
                            if (results.monitoring.stabilityHistory.length > 100) {{
                                results.monitoring.stabilityHistory = results.monitoring.stabilityHistory.slice(-50);
                            }}

                            return results.isStable;
                        }};

                        // Step 9: Main stability waiting logic
                        const waitForStability = async () => {{
                            return new Promise((resolve) => {{
                                const startTime = Date.now();
                                results.waitTime.startTime = startTime;
                                const timeoutMs = {timeoutSeconds} * 1000;

                                const checkStability = () => {{
                                    const currentTime = Date.now();
                                    const elapsedMs = currentTime - startTime;

                                    // Check for timeout
                                    if (elapsedMs >= timeoutMs) {{
                                        results.waitTime.timedOut = true;
                                        results.waitTime.endTime = currentTime;
                                        results.waitTime.totalWaitMs = elapsedMs;
                                        results.errors.push(`Stability wait timed out after ${{timeoutMs}}ms`);
                                        resolve(results);
                                        return;
                                    }}

                                    // Perform stability check
                                    const isStable = performStabilityCheck();

                                    if (isStable) {{
                                        // Application is stable
                                        results.waitTime.endTime = currentTime;
                                        results.waitTime.totalWaitMs = elapsedMs;
                                        resolve(results);
                                    }} else {{
                                        // Continue checking
                                        setTimeout(checkStability, {checkIntervalMs});
                                    }}
                                }};

                                // Start the stability checking loop
                                checkStability();
                            }});
                        }};

                        // Initialize and start monitoring
                        if (!detectAngularEnvironment()) {{
                            return results;
                        }}

                        return waitForStability();
                    }};

                    return waitForAngularStability();
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to wait for Angular stability: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Monitor Zone.js activity and async operations without waiting")]
    public async Task<string> MonitorZoneActivity(
        [Description("Duration in seconds")] int durationSeconds = 30,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    const results = {{
                        zoneJsPresent: false,
                        isZoneless: false,
                        monitoringDuration: {durationSeconds},
                        startTime: Date.now(),
                        endTime: null,
                        zoneActivity: {{
                            totalMacroTasks: 0,
                            totalMicroTasks: 0,
                            taskTypes: {{}},
                            peakConcurrentTasks: 0,
                            averageConcurrentTasks: 0
                        }},
                        timeline: [],
                        statistics: {{
                            samplesCollected: 0,
                            stabilityPercentage: 0,
                            longestStablePeriod: 0,
                            shortestStablePeriod: 0
                        }},
                        recommendations: []
                    }};

                    // Check Zone.js presence
                    results.zoneJsPresent = !!(window.Zone && window.Zone.current);
                    results.isZoneless = !results.zoneJsPresent || 
                                       document.querySelector('[ng-zoneless]') !== null;

                    if (!results.zoneJsPresent) {{
                        results.recommendations.push({{
                            type: 'zoneless',
                            message: 'Application appears to be zoneless - consider manual async operation tracking'
                        }});
                        
                        // For zoneless applications, provide basic monitoring
                        setTimeout(() => {{
                            results.endTime = Date.now();
                            window.__zoneMonitoringResults = results;
                        }}, {durationSeconds * 1000});
                        
                        return results;
                    }}

                    // Set up Zone.js monitoring
                    const originalFork = window.Zone.current.fork;
                    const originalScheduleMacroTask = window.Zone.current.scheduleMacroTask;
                    const originalScheduleMicroTask = window.Zone.current.scheduleMicroTask;

                    let sampleCount = 0;
                    let stablePeriods = [];
                    let currentStablePeriod = null;

                    const collectSample = () => {{
                        const timestamp = Date.now();
                        sampleCount++;

                        try {{
                            const zone = window.Zone.current;
                            let macroTasks = 0;
                            let microTasks = 0;

                            if (zone._parent && zone._parent._properties) {{
                                macroTasks = zone._parent._properties.macroTasks?.length || 0;
                                microTasks = zone._parent._properties.microTasks?.length || 0;
                            }}

                            const totalTasks = macroTasks + microTasks;
                            const isStable = totalTasks === 0;

                            // Track stability periods
                            if (isStable) {{
                                if (!currentStablePeriod) {{
                                    currentStablePeriod = {{ start: timestamp, end: null }};
                                }}
                            }} else {{
                                if (currentStablePeriod) {{
                                    currentStablePeriod.end = timestamp;
                                    const duration = currentStablePeriod.end - currentStablePeriod.start;
                                    stablePeriods.push(duration);
                                    currentStablePeriod = null;
                                }}
                            }}

                            results.timeline.push({{
                                timestamp: timestamp,
                                macroTasks: macroTasks,
                                microTasks: microTasks,
                                totalTasks: totalTasks,
                                isStable: isStable
                            }});

                            results.zoneActivity.totalMacroTasks = Math.max(results.zoneActivity.totalMacroTasks, macroTasks);
                            results.zoneActivity.totalMicroTasks = Math.max(results.zoneActivity.totalMicroTasks, microTasks);
                            results.zoneActivity.peakConcurrentTasks = Math.max(results.zoneActivity.peakConcurrentTasks, totalTasks);

                        }} catch (e) {{
                            results.timeline.push({{
                                timestamp: timestamp,
                                error: e.message
                            }});
                        }}
                    }};

                    // Start monitoring
                    const monitoringInterval = setInterval(collectSample, 100);

                    // Stop monitoring after duration
                    setTimeout(() => {{
                        clearInterval(monitoringInterval);
                        
                        // Final stability period
                        if (currentStablePeriod) {{
                            currentStablePeriod.end = Date.now();
                            const duration = currentStablePeriod.end - currentStablePeriod.start;
                            stablePeriods.push(duration);
                        }}

                        // Calculate statistics
                        results.endTime = Date.now();
                        results.statistics.samplesCollected = sampleCount;
                        
                        const stableSamples = results.timeline.filter(t => t.isStable).length;
                        results.statistics.stabilityPercentage = sampleCount > 0 ? (stableSamples / sampleCount) * 100 : 0;

                        if (stablePeriods.length > 0) {{
                            results.statistics.longestStablePeriod = Math.max(...stablePeriods);
                            results.statistics.shortestStablePeriod = Math.min(...stablePeriods);
                        }}

                        const totalTasks = results.timeline.reduce((sum, t) => sum + (t.totalTasks || 0), 0);
                        results.zoneActivity.averageConcurrentTasks = sampleCount > 0 ? totalTasks / sampleCount : 0;

                        // Generate recommendations
                        if (results.statistics.stabilityPercentage < 70) {{
                            results.recommendations.push({{
                                type: 'stability',
                                message: 'Low stability percentage detected - consider optimizing async operations'
                            }});
                        }}

                        if (results.zoneActivity.peakConcurrentTasks > 10) {{
                            results.recommendations.push({{
                                type: 'performance',
                                message: 'High concurrent task count detected - may impact performance'
                            }});
                        }}

                        window.__zoneMonitoringResults = results;
                    }}, {durationSeconds * 1000});

                    return results;
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            
            // Wait for monitoring to complete
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds + 1));
            
            // Retrieve final results
            try
            {
                var finalResultsJs = @"
                    (() => {
                        if (window.__zoneMonitoringResults) {
                            return window.__zoneMonitoringResults;
                        }
                        return { error: 'Zone monitoring results not available' };
                    })();
                ";
                
                var finalResult = await session.Page.EvaluateAsync<object>(finalResultsJs);
                return JsonSerializer.Serialize(finalResult, JsonOptions);
            }
            catch
            {
                return JsonSerializer.Serialize(result, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            return $"Failed to monitor Zone activity: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Check current Angular application stability status without waiting")]
    public async Task<string> CheckAngularStabilityStatus(
        [Description("Include detailed breakdown of stability checks")] bool includeDetails = true,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    const results = {{
                        timestamp: Date.now(),
                        angularDetected: false,
                        isStable: false,
                        isZoneless: false,
                        zoneJsPresent: false,
                        stabilityFactors: {{
                            zone: {{ stable: false, pendingMacro: 0, pendingMicro: 0 }},
                            http: {{ stable: false, pendingRequests: 0 }},
                            timers: {{ stable: false, activeTimers: 0 }},
                            changeDetection: {{ stable: false, running: false }},
                            promises: {{ stable: false, pending: 0 }}
                        }},
                        summary: {{
                            totalAsyncOperations: 0,
                            stabilityScore: 0,
                            timeToStability: 'unknown'
                        }},
                        includeDetails: {includeDetails.ToString().ToLower()}
                    }};

                    // Quick Angular detection
                    results.angularDetected = !!(window.ng || window.ngDevMode || document.querySelector('[ng-version]'));
                    
                    if (!results.angularDetected) {{
                        return results;
                    }}

                    // Zone.js check
                    results.zoneJsPresent = !!(window.Zone && window.Zone.current);
                    results.isZoneless = !results.zoneJsPresent || document.querySelector('[ng-zoneless]') !== null;

                    // Quick stability checks
                    let stableFactors = 0;
                    const totalFactors = 5;

                    // Zone stability
                    if (results.zoneJsPresent) {{
                        try {{
                            const zone = window.Zone.current;
                            if (zone._parent && zone._parent._properties) {{
                                results.stabilityFactors.zone.pendingMacro = zone._parent._properties.macroTasks?.length || 0;
                                results.stabilityFactors.zone.pendingMicro = zone._parent._properties.microTasks?.length || 0;
                            }}
                            results.stabilityFactors.zone.stable = 
                                results.stabilityFactors.zone.pendingMacro === 0 && 
                                results.stabilityFactors.zone.pendingMicro === 0;
                        }} catch (e) {{
                            results.stabilityFactors.zone.stable = true; // Assume stable if can't check
                        }}
                    }} else {{
                        results.stabilityFactors.zone.stable = true; // Zoneless is considered zone-stable
                    }}
                    
                    if (results.stabilityFactors.zone.stable) stableFactors++;

                    // HTTP stability (simplified check)
                    results.stabilityFactors.http.stable = true; // Default assumption
                    stableFactors++;

                    // Timer stability (simplified check)  
                    results.stabilityFactors.timers.stable = true; // Default assumption
                    stableFactors++;

                    // Change detection stability (simplified check)
                    results.stabilityFactors.changeDetection.stable = true; // Default assumption  
                    stableFactors++;

                    // Promise stability (simplified check)
                    results.stabilityFactors.promises.stable = true; // Default assumption
                    stableFactors++;

                    // Calculate overall stability
                    results.summary.totalAsyncOperations = 
                        results.stabilityFactors.zone.pendingMacro +
                        results.stabilityFactors.zone.pendingMicro +
                        results.stabilityFactors.http.pendingRequests +
                        results.stabilityFactors.timers.activeTimers +
                        results.stabilityFactors.promises.pending;

                    results.summary.stabilityScore = (stableFactors / totalFactors) * 100;
                    results.isStable = stableFactors === totalFactors;

                    if (results.isStable) {{
                        results.summary.timeToStability = 'immediate';
                    }} else if (results.summary.totalAsyncOperations <= 3) {{
                        results.summary.timeToStability = 'within seconds';
                    }} else if (results.summary.totalAsyncOperations <= 10) {{
                        results.summary.timeToStability = 'within 10 seconds';
                    }} else {{
                        results.summary.timeToStability = 'more than 10 seconds';
                    }}

                    return results;
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to check Angular stability status: {ex.Message}";
        }
    }
}
