using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// ANG-005: Component Lifecycle Monitoring
/// Implements trace_component_lifecycle_hooks function for monitoring Angular component lifecycle execution
/// </summary>
[McpServerToolType]
public class AngularLifecycleMonitor(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Monitor Angular component lifecycle hooks execution with timing and performance analysis")]
    public async Task<string> TraceComponentLifecycleHooks(
        [Description("Duration in seconds to monitor lifecycle hooks")] int durationSeconds = 30,
        [Description("Maximum number of components to monitor")] int maxComponents = 25,
        [Description("Include hook execution timing details")] bool includeTimingDetails = true,
        [Description("Monitor specific lifecycle hooks (comma-separated: ngOnInit,ngOnDestroy,ngOnChanges,ngAfterViewInit,ngAfterViewChecked,ngAfterContentInit,ngAfterContentChecked,ngDoCheck)")] string? specificHooks = null,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string hooksToMonitor = string.IsNullOrEmpty(specificHooks) 
                ? "ngOnInit,ngOnDestroy,ngOnChanges,ngAfterViewInit,ngAfterViewChecked,ngAfterContentInit,ngAfterContentChecked,ngDoCheck"
                : specificHooks;

            var jsCode = $@"
                (() => {{
                    // ANG-005: Component Lifecycle Monitoring Implementation
                    const traceComponentLifecycleHooks = () => {{
                        const results = {{
                            angularDetected: false,
                            monitoringStarted: false,
                            monitoringDuration: {durationSeconds},
                            maxComponents: {maxComponents},
                            includeTimingDetails: {includeTimingDetails.ToString().ToLower()},
                            startTime: Date.now(),
                            endTime: null,
                            monitoring: {{
                                targetHooks: '{hooksToMonitor}'.split(',').map(h => h.trim()),
                                componentsMonitored: [],
                                lifecycleEvents: [],
                                hookExecutions: [],
                                performanceMetrics: {{}}
                            }},
                            statistics: {{
                                totalComponents: 0,
                                totalHookExecutions: 0,
                                averageExecutionTime: 0,
                                slowestHook: null,
                                fastestHook: null,
                                hookExecutionsByType: {{}},
                                executionTimeByHook: {{}},
                                componentsByType: {{}}
                            }},
                            analysis: {{
                                performanceIssues: [],
                                recommendations: [],
                                executionPatterns: [],
                                memoryLeakRisks: []
                            }},
                            timeline: []
                        }};

                        // Step 1: Check for Angular and component detection capabilities
                        const checkAngularSupport = () => {{
                            if (!window.ng) {{
                                results.error = 'Angular not detected or not in development mode';
                                return false;
                            }}

                            results.angularDetected = true;

                            // Check for component inspection capabilities
                            if (!window.ng.getComponent) {{
                                results.error = 'Angular component inspection not available';
                                return false;
                            }}

                            return true;
                        }};

                        if (!checkAngularSupport()) {{
                            return results;
                        }}

                        // Step 2: Discover components and their lifecycle hooks
                        const discoverComponents = () => {{
                            const discoveredComponents = [];
                            
                            // Find all Angular component elements
                            const potentialComponents = Array.from(document.querySelectorAll('*'))
                                .filter(el => {{
                                    // Check for Angular-specific attributes
                                    return Array.from(el.attributes).some(attr => 
                                        attr.name.startsWith('_nghost') || 
                                        attr.name.startsWith('ng-reflect') ||
                                        attr.name.startsWith('ng-version'));
                                }})
                                .slice(0, {maxComponents}); // Limit for performance

                            potentialComponents.forEach((el, index) => {{
                                try {{
                                    const component = window.ng.getComponent(el);
                                    if (component) {{
                                        const componentInfo = {{
                                            id: `component-${{index}}`,
                                            element: el.tagName.toLowerCase(),
                                            componentName: component.constructor.name,
                                            selector: el.getAttribute('ng-reflect-constructor') || el.tagName.toLowerCase(),
                                            elementIndex: index,
                                            instance: component,
                                            element: el,
                                            lifecycleHooks: {{}},
                                            monitoredHooks: [],
                                            executionHistory: [],
                                            isStandalone: !!(component.constructor.ɵcmp?.standalone),
                                            hasOnPush: component.constructor.ɵcmp?.changeDetection === 0,
                                            hasSignals: false
                                        }};

                                        // Check for signals in component (Angular 16+)
                                        Object.getOwnPropertyNames(component).forEach(prop => {{
                                            try {{
                                                const value = component[prop];
                                                if (value && typeof value === 'function' && 
                                                    (value.ɵIsSignal || 
                                                     (value.constructor && value.constructor.name === 'SignalImpl'))) {{
                                                    componentInfo.hasSignals = true;
                                                }}
                                            }} catch (e) {{
                                                // Skip inaccessible properties
                                            }}
                                        }});

                                        // Check which lifecycle hooks are implemented
                                        results.monitoring.targetHooks.forEach(hookName => {{
                                            if (typeof component[hookName] === 'function') {{
                                                componentInfo.lifecycleHooks[hookName] = {{
                                                    implemented: true,
                                                    executionCount: 0,
                                                    totalExecutionTime: 0,
                                                    averageExecutionTime: 0,
                                                    minExecutionTime: Number.MAX_SAFE_INTEGER,
                                                    maxExecutionTime: 0,
                                                    lastExecuted: null
                                                }};
                                            }}
                                        }});

                                        discoveredComponents.push(componentInfo);
                                    }}
                                }} catch (e) {{
                                    // Skip elements without valid components
                                }}
                            }});

                            results.monitoring.componentsMonitored = discoveredComponents;
                            results.statistics.totalComponents = discoveredComponents.length;

                            // Count components by type
                            discoveredComponents.forEach(comp => {{
                                const type = comp.componentName;
                                results.statistics.componentsByType[type] = 
                                    (results.statistics.componentsByType[type] || 0) + 1;
                            }});

                            return discoveredComponents;
                        }};

                        // Step 3: Set up lifecycle hook monitoring with method wrapping
                        const setupLifecycleMonitoring = (components) => {{
                            const executionLog = [];
                            
                            components.forEach(componentInfo => {{
                                const component = componentInfo.instance;
                                
                                Object.keys(componentInfo.lifecycleHooks).forEach(hookName => {{
                                    try {{
                                        const originalMethod = component[hookName].bind(component);
                                        
                                        // Wrap the lifecycle hook method
                                        component[hookName] = function(...args) {{
                                            const timestamp = Date.now();
                                            const startTime = performance.now();
                                            
                                            const executionEvent = {{
                                                componentId: componentInfo.id,
                                                componentName: componentInfo.componentName,
                                                hookName: hookName,
                                                timestamp: timestamp,
                                                startTime: startTime,
                                                endTime: null,
                                                executionTime: null,
                                                args: args.length > 0 ? JSON.stringify(args).substring(0, 200) : null,
                                                success: true,
                                                error: null,
                                                callStack: new Error().stack?.split('\\n').slice(1, 5).join('\\n') || 'unavailable'
                                            }};

                                            try {{
                                                // Execute original method
                                                const result = originalMethod.apply(this, args);
                                                
                                                const endTime = performance.now();
                                                const executionTime = endTime - startTime;
                                                
                                                executionEvent.endTime = endTime;
                                                executionEvent.executionTime = executionTime;
                                                
                                                // Update statistics
                                                const hookStats = componentInfo.lifecycleHooks[hookName];
                                                hookStats.executionCount++;
                                                hookStats.totalExecutionTime += executionTime;
                                                hookStats.averageExecutionTime = hookStats.totalExecutionTime / hookStats.executionCount;
                                                hookStats.minExecutionTime = Math.min(hookStats.minExecutionTime, executionTime);
                                                hookStats.maxExecutionTime = Math.max(hookStats.maxExecutionTime, executionTime);
                                                hookStats.lastExecuted = timestamp;
                                                
                                                // Update global statistics
                                                results.statistics.totalHookExecutions++;
                                                results.statistics.hookExecutionsByType[hookName] = 
                                                    (results.statistics.hookExecutionsByType[hookName] || 0) + 1;
                                                
                                                if (!results.statistics.executionTimeByHook[hookName]) {{
                                                    results.statistics.executionTimeByHook[hookName] = {{
                                                        total: 0,
                                                        count: 0,
                                                        average: 0,
                                                        min: Number.MAX_SAFE_INTEGER,
                                                        max: 0
                                                    }};
                                                }}
                                                
                                                const globalHookStats = results.statistics.executionTimeByHook[hookName];
                                                globalHookStats.total += executionTime;
                                                globalHookStats.count++;
                                                globalHookStats.average = globalHookStats.total / globalHookStats.count;
                                                globalHookStats.min = Math.min(globalHookStats.min, executionTime);
                                                globalHookStats.max = Math.max(globalHookStats.max, executionTime);
                                                
                                                executionLog.push(executionEvent);
                                                results.timeline.push(executionEvent);
                                                componentInfo.executionHistory.push(executionEvent);
                                                
                                                return result;
                                            }} catch (error) {{
                                                const endTime = performance.now();
                                                executionEvent.endTime = endTime;
                                                executionEvent.executionTime = endTime - startTime;
                                                executionEvent.success = false;
                                                executionEvent.error = error.message || error.toString();
                                                
                                                executionLog.push(executionEvent);
                                                results.timeline.push(executionEvent);
                                                componentInfo.executionHistory.push(executionEvent);
                                                
                                                throw error; // Re-throw to maintain original behavior
                                            }}
                                        }};
                                        
                                        componentInfo.monitoredHooks.push(hookName);
                                    }} catch (e) {{
                                        console.warn(`Failed to monitor lifecycle hook ${{hookName}} for component ${{componentInfo.componentName}}:`, e);
                                    }}
                                }});
                            }});
                            
                            results.monitoring.hookExecutions = executionLog;
                            return executionLog;
                        }};

                        // Step 4: Performance analysis and issue detection
                        const analyzePerformance = () => {{
                            const performanceIssues = [];
                            const recommendations = [];
                            const executionPatterns = [];
                            const memoryLeakRisks = [];
                            
                            // Analyze execution times
                            Object.keys(results.statistics.executionTimeByHook).forEach(hookName => {{
                                const stats = results.statistics.executionTimeByHook[hookName];
                                
                                // Detect slow hooks (> 10ms is considered slow for lifecycle hooks)
                                if (stats.average > 10) {{
                                    performanceIssues.push({{
                                        type: 'slow_lifecycle_hook',
                                        severity: stats.average > 50 ? 'high' : 'medium',
                                        hookName: hookName,
                                        averageTime: stats.average,
                                        maxTime: stats.max,
                                        message: `${{hookName}} hook has slow average execution time (${{stats.average.toFixed(2)}}ms)`
                                    }});
                                }}
                                
                                // Detect frequently called hooks that might cause performance issues
                                if (stats.count > 100) {{
                                    performanceIssues.push({{
                                        type: 'frequent_hook_execution',
                                        severity: 'medium',
                                        hookName: hookName,
                                        executionCount: stats.count,
                                        message: `${{hookName}} hook executed ${{stats.count}} times during monitoring`
                                    }});
                                }}
                            }});
                            
                            // Analyze component patterns
                            results.monitoring.componentsMonitored.forEach(comp => {{
                                // Check for potential memory leak patterns
                                if (comp.lifecycleHooks.ngOnDestroy && 
                                    comp.lifecycleHooks.ngOnDestroy.executionCount === 0 && 
                                    comp.lifecycleHooks.ngOnInit && 
                                    comp.lifecycleHooks.ngOnInit.executionCount > 0) {{
                                    memoryLeakRisks.push({{
                                        componentName: comp.componentName,
                                        risk: 'ngOnDestroy not called',
                                        message: 'Component may have memory leaks - ngOnInit called but ngOnDestroy not executed',
                                        severity: 'high'
                                    }});
                                }}
                                
                                // Check for change detection patterns
                                if (comp.lifecycleHooks.ngDoCheck && 
                                    comp.lifecycleHooks.ngDoCheck.executionCount > 50) {{
                                    performanceIssues.push({{
                                        type: 'excessive_change_detection',
                                        severity: 'medium',
                                        componentName: comp.componentName,
                                        executionCount: comp.lifecycleHooks.ngDoCheck.executionCount,
                                        message: 'ngDoCheck called excessively - consider OnPush change detection'
                                    }});
                                }}
                            }});
                            
                            // Generate recommendations
                            if (performanceIssues.length > 0) {{
                                recommendations.push({{
                                    type: 'performance',
                                    priority: 'high',
                                    message: 'Optimize slow lifecycle hooks',
                                    details: 'Consider moving heavy computations to ngAfterViewInit or using async operations'
                                }});
                            }}
                            
                            if (memoryLeakRisks.length > 0) {{
                                recommendations.push({{
                                    type: 'memory',
                                    priority: 'critical',
                                    message: 'Implement proper cleanup in ngOnDestroy',
                                    details: 'Ensure subscriptions are unsubscribed and resources are cleaned up'
                                }});
                            }}
                            
                            const excessiveChangeDetection = results.monitoring.componentsMonitored
                                .filter(c => c.lifecycleHooks.ngDoCheck?.executionCount > 20).length;
                            
                            if (excessiveChangeDetection > 0) {{
                                recommendations.push({{
                                    type: 'change_detection',
                                    priority: 'medium',
                                    message: 'Consider OnPush change detection strategy',
                                    details: `${{excessiveChangeDetection}} components with frequent change detection cycles`
                                }});
                            }}
                            
                            results.analysis.performanceIssues = performanceIssues;
                            results.analysis.recommendations = recommendations;
                            results.analysis.executionPatterns = executionPatterns;
                            results.analysis.memoryLeakRisks = memoryLeakRisks;
                        }};

                        // Step 5: Calculate final statistics
                        const calculateFinalStatistics = () => {{
                            if (results.statistics.totalHookExecutions > 0) {{
                                const allExecutionTimes = results.monitoring.hookExecutions
                                    .filter(e => e.executionTime !== null)
                                    .map(e => e.executionTime);
                                
                                if (allExecutionTimes.length > 0) {{
                                    results.statistics.averageExecutionTime = 
                                        allExecutionTimes.reduce((a, b) => a + b, 0) / allExecutionTimes.length;
                                    
                                    const maxTime = Math.max(...allExecutionTimes);
                                    const minTime = Math.min(...allExecutionTimes);
                                    
                                    const slowestExecution = results.monitoring.hookExecutions
                                        .find(e => e.executionTime === maxTime);
                                    const fastestExecution = results.monitoring.hookExecutions
                                        .find(e => e.executionTime === minTime);
                                    
                                    results.statistics.slowestHook = {{
                                        hook: slowestExecution.hookName,
                                        component: slowestExecution.componentName,
                                        time: maxTime,
                                        timestamp: slowestExecution.timestamp
                                    }};
                                    
                                    results.statistics.fastestHook = {{
                                        hook: fastestExecution.hookName,
                                        component: fastestExecution.componentName,
                                        time: minTime,
                                        timestamp: fastestExecution.timestamp
                                    }};
                                }}
                            }}
                        }};

                        // Step 6: Execute monitoring
                        const executeMonitoring = () => {{
                            results.monitoringStarted = true;
                            
                            const discoveredComponents = discoverComponents();
                            if (discoveredComponents.length === 0) {{
                                results.error = 'No Angular components detected for lifecycle monitoring';
                                return results;
                            }}
                            
                            setupLifecycleMonitoring(discoveredComponents);
                            
                            // Set up completion timer
                            setTimeout(() => {{
                                results.endTime = Date.now();
                                results.monitoringDuration = (results.endTime - results.startTime) / 1000;
                                
                                analyzePerformance();
                                calculateFinalStatistics();
                                
                                // Store results globally for retrieval
                                window.__angularLifecycleMonitoringResults = results;
                            }}, {durationSeconds * 1000});
                            
                            return results;
                        }};

                        return executeMonitoring();
                    }};

                    return traceComponentLifecycleHooks();
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
                        if (window.__angularLifecycleMonitoringResults) {
                            return window.__angularLifecycleMonitoringResults;
                        }
                        return { error: 'Lifecycle monitoring results not available' };
                    })();
                ";
                
                var finalResult = await session.Page.EvaluateAsync<object>(finalResultsJs);
                return JsonSerializer.Serialize(finalResult, JsonOptions);
            }
            catch
            {
                // Return initial result if final results aren't available
                return JsonSerializer.Serialize(result, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            return $"Failed to trace component lifecycle hooks: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get quick lifecycle monitoring status for components without full monitoring")]
    public async Task<string> CheckComponentLifecycleStatus(
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = @"
                (() => {
                    const results = {
                        angularDetected: false,
                        componentsFound: 0,
                        lifecycleHooksImplemented: {},
                        componentTypes: {},
                        standalonComponents: 0,
                        onPushComponents: 0,
                        signalComponents: 0
                    };

                    if (!window.ng) {
                        results.error = 'Angular not detected';
                        return results;
                    }

                    results.angularDetected = true;

                    if (!window.ng.getComponent) {
                        results.error = 'Component inspection not available';
                        return results;
                    }

                    const lifecycleHooks = [
                        'ngOnInit', 'ngOnDestroy', 'ngOnChanges', 'ngAfterViewInit', 
                        'ngAfterViewChecked', 'ngAfterContentInit', 'ngAfterContentChecked', 'ngDoCheck'
                    ];

                    lifecycleHooks.forEach(hook => {
                        results.lifecycleHooksImplemented[hook] = 0;
                    });

                    const components = Array.from(document.querySelectorAll('*'))
                        .filter(el => Array.from(el.attributes).some(attr => 
                            attr.name.startsWith('_nghost') || attr.name.startsWith('ng-reflect')))
                        .slice(0, 50);

                    components.forEach(el => {
                        try {
                            const component = window.ng.getComponent(el);
                            if (component) {
                                results.componentsFound++;
                                
                                const componentName = component.constructor.name;
                                results.componentTypes[componentName] = (results.componentTypes[componentName] || 0) + 1;
                                
                                // Check for standalone components
                                if (component.constructor.ɵcmp?.standalone) {
                                    results.standalonComponents++;
                                }
                                
                                // Check for OnPush change detection
                                if (component.constructor.ɵcmp?.changeDetection === 0) {
                                    results.onPushComponents++;
                                }
                                
                                // Check for signals
                                let hasSignals = false;
                                Object.getOwnPropertyNames(component).forEach(prop => {
                                    try {
                                        const value = component[prop];
                                        if (value && typeof value === 'function' && 
                                            (value.ɵIsSignal || (value.constructor && value.constructor.name === 'SignalImpl'))) {
                                            hasSignals = true;
                                        }
                                    } catch (e) {
                                        // Skip
                                    }
                                });
                                
                                if (hasSignals) {
                                    results.signalComponents++;
                                }
                                
                                // Check which lifecycle hooks are implemented
                                lifecycleHooks.forEach(hook => {
                                    if (typeof component[hook] === 'function') {
                                        results.lifecycleHooksImplemented[hook]++;
                                    }
                                });
                            }
                        } catch (e) {
                            // Skip
                        }
                    });

                    return results;
                })();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to check component lifecycle status: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Monitor specific lifecycle hook execution patterns for performance optimization")]
    public async Task<string> AnalyzeLifecycleHookPatterns(
        [Description("Specific lifecycle hook to analyze (ngOnInit, ngOnDestroy, ngDoCheck, etc.)")] string hookName,
        [Description("Duration in seconds to monitor the specific hook")] int durationSeconds = 15,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    const hookName = '{hookName}';
                    const results = {{
                        hookName: hookName,
                        angularDetected: false,
                        monitoringStarted: false,
                        monitoringDuration: {durationSeconds},
                        startTime: Date.now(),
                        endTime: null,
                        executions: [],
                        statistics: {{
                            totalExecutions: 0,
                            uniqueComponents: 0,
                            averageExecutionTime: 0,
                            medianExecutionTime: 0,
                            minExecutionTime: Number.MAX_SAFE_INTEGER,
                            maxExecutionTime: 0,
                            standardDeviation: 0,
                            executionsPerSecond: 0
                        }},
                        patterns: {{
                            executionClusters: [],
                            outliers: [],
                            trends: []
                        }},
                        recommendations: []
                    }};

                    if (!window.ng || !window.ng.getComponent) {{
                        results.error = 'Angular component inspection not available';
                        return results;
                    }}

                    results.angularDetected = true;
                    results.monitoringStarted = true;

                    const componentsToMonitor = [];
                    const executionLog = [];

                    // Find components with the target lifecycle hook
                    Array.from(document.querySelectorAll('*'))
                        .filter(el => Array.from(el.attributes).some(attr => 
                            attr.name.startsWith('_nghost') || attr.name.startsWith('ng-reflect')))
                        .slice(0, 30)
                        .forEach((el, index) => {{
                            try {{
                                const component = window.ng.getComponent(el);
                                if (component && typeof component[hookName] === 'function') {{
                                    const componentInfo = {{
                                        id: `comp-${{index}}`,
                                        name: component.constructor.name,
                                        element: el.tagName.toLowerCase(),
                                        originalMethod: component[hookName].bind(component)
                                    }};

                                    // Wrap the specific hook method
                                    component[hookName] = function(...args) {{
                                        const startTime = performance.now();
                                        const timestamp = Date.now();

                                        try {{
                                            const result = componentInfo.originalMethod.apply(this, args);
                                            const endTime = performance.now();
                                            const executionTime = endTime - startTime;

                                            const execution = {{
                                                componentId: componentInfo.id,
                                                componentName: componentInfo.name,
                                                timestamp: timestamp,
                                                executionTime: executionTime,
                                                success: true,
                                                args: args.length > 0 ? args.length : 0
                                            }};

                                            executionLog.push(execution);
                                            return result;
                                        }} catch (error) {{
                                            const endTime = performance.now();
                                            const executionTime = endTime - startTime;

                                            const execution = {{
                                                componentId: componentInfo.id,
                                                componentName: componentInfo.name,
                                                timestamp: timestamp,
                                                executionTime: executionTime,
                                                success: false,
                                                error: error.message,
                                                args: args.length > 0 ? args.length : 0
                                            }};

                                            executionLog.push(execution);
                                            throw error;
                                        }}
                                    }};

                                    componentsToMonitor.push(componentInfo);
                                }}
                            }} catch (e) {{
                                // Skip
                            }}
                        }});

                    // Monitor for specified duration
                    setTimeout(() => {{
                        results.endTime = Date.now();
                        results.monitoringDuration = (results.endTime - results.startTime) / 1000;
                        results.executions = executionLog;

                        // Calculate statistics
                        if (executionLog.length > 0) {{
                            const executionTimes = executionLog.map(e => e.executionTime);
                            const uniqueComponents = new Set(executionLog.map(e => e.componentName)).size;

                            results.statistics.totalExecutions = executionLog.length;
                            results.statistics.uniqueComponents = uniqueComponents;
                            results.statistics.executionsPerSecond = executionLog.length / results.monitoringDuration;
                            results.statistics.averageExecutionTime = executionTimes.reduce((a, b) => a + b, 0) / executionTimes.length;
                            results.statistics.minExecutionTime = Math.min(...executionTimes);
                            results.statistics.maxExecutionTime = Math.max(...executionTimes);

                            // Calculate median
                            const sortedTimes = executionTimes.sort((a, b) => a - b);
                            const mid = Math.floor(sortedTimes.length / 2);
                            results.statistics.medianExecutionTime = sortedTimes.length % 2 === 0
                                ? (sortedTimes[mid - 1] + sortedTimes[mid]) / 2
                                : sortedTimes[mid];

                            // Calculate standard deviation
                            const variance = executionTimes.reduce((acc, time) => {{
                                return acc + Math.pow(time - results.statistics.averageExecutionTime, 2);
                            }}, 0) / executionTimes.length;
                            results.statistics.standardDeviation = Math.sqrt(variance);

                            // Identify outliers (execution times > 2 standard deviations from mean)
                            const threshold = results.statistics.averageExecutionTime + (2 * results.statistics.standardDeviation);
                            results.patterns.outliers = executionLog.filter(e => e.executionTime > threshold);

                            // Generate recommendations
                            if (results.statistics.averageExecutionTime > 5) {{
                                results.recommendations.push({{
                                    type: 'performance',
                                    severity: 'medium',
                                    message: `${{hookName}} has slow average execution time (${{results.statistics.averageExecutionTime.toFixed(2)}}ms)`,
                                    suggestion: 'Consider optimizing logic in this lifecycle hook'
                                }});
                            }}

                            if (results.patterns.outliers.length > 0) {{
                                results.recommendations.push({{
                                    type: 'consistency',
                                    severity: 'low',
                                    message: `${{results.patterns.outliers.length}} outlier executions detected`,
                                    suggestion: 'Investigate components with inconsistent execution times'
                                }});
                            }}

                            if (results.statistics.executionsPerSecond > 20) {{
                                results.recommendations.push({{
                                    type: 'frequency',
                                    severity: 'high',
                                    message: `${{hookName}} executing very frequently (${{results.statistics.executionsPerSecond.toFixed(1)}} times/sec)`,
                                    suggestion: 'Consider OnPush change detection or debouncing strategies'
                                }});
                            }}
                        }}

                        window.__angularLifecyclePatternResults = results;
                    }}, {durationSeconds * 1000});

                    return {{ 
                        message: `Monitoring ${{hookName}} on ${{componentsToMonitor.length}} components for {durationSeconds} seconds...`,
                        componentsFound: componentsToMonitor.length,
                        hookName: hookName
                    }};
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
                        if (window.__angularLifecyclePatternResults) {
                            return window.__angularLifecyclePatternResults;
                        }
                        return { error: 'Lifecycle pattern analysis results not available' };
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
            return $"Failed to analyze lifecycle hook patterns: {ex.Message}";
        }
    }
}
