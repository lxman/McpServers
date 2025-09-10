using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// ANG-014: Change Detection Bottleneck Detection
/// Implements detect_change_detection_bottlenecks function for identifying and analyzing change detection performance issues
/// Combines insights from lifecycle monitoring and performance profiling to detect change detection bottlenecks
/// </summary>
[McpServerToolType]
public class AngularChangeDetectionAnalyzer(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Detect and analyze change detection bottlenecks in Angular applications")]
    public async Task<string> DetectChangeDetectionBottlenecks(
        [Description("Duration in seconds to analyze change detection patterns")] int durationSeconds = 30,
        [Description("Maximum number of components to analyze")] int maxComponents = 25,
        [Description("Include detailed bottleneck analysis")] bool includeDetailedAnalysis = true,
        [Description("Severity threshold for reporting bottlenecks (low, medium, high)")] string severityThreshold = "medium",
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    // ANG-014: Change Detection Bottleneck Detection Implementation
                    const detectChangeDetectionBottlenecks = () => {{
                        const results = {{
                            angularDetected: false,
                            analysisStarted: false,
                            analysisDuration: {durationSeconds},
                            maxComponents: {maxComponents},
                            includeDetailedAnalysis: {includeDetailedAnalysis.ToString().ToLower()},
                            severityThreshold: '{severityThreshold}',
                            startTime: Date.now(),
                            endTime: null,
                            changeDetectionMetrics: {{
                                totalCycles: 0,
                                averageCycleTime: 0,
                                slowestCycle: 0,
                                fastestCycle: Number.MAX_SAFE_INTEGER,
                                cyclesPerSecond: 0,
                                excessiveCycles: 0
                            }},
                            componentAnalysis: {{
                                componentsAnalyzed: 0,
                                problematicComponents: [],
                                onPushComponents: 0,
                                defaultComponents: 0,
                                heavyLifecycleComponents: []
                            }},
                            bottlenecks: {{
                                critical: [],
                                major: [],
                                minor: [],
                                summary: {{
                                    totalBottlenecks: 0,
                                    criticalCount: 0,
                                    majorCount: 0,
                                    minorCount: 0
                                }}
                            }},
                            zoneAnalysis: {{
                                zoneJsDetected: false,
                                excessiveAsyncOps: false,
                                longRunningTasks: [],
                                memoryLeakRisks: []
                            }},
                            performanceImpact: {{
                                estimatedImpact: 'unknown',
                                fpsMeasurement: 0,
                                userExperienceScore: 0,
                                renderingDelays: []
                            }},
                            optimizationRecommendations: [],
                            detailedFindings: [],
                            timeline: []
                        }};

                        // Step 1: Verify Angular and required APIs
                        const initializeAnalysis = () => {{
                            if (!window.ng) {{
                                results.error = 'Angular not detected or not in development mode. Change detection analysis requires Angular DevTools API.';
                                return false;
                            }}

                            results.angularDetected = true;

                            // Check for Zone.js
                            if (window.Zone) {{
                                results.zoneAnalysis.zoneJsDetected = true;
                            }}

                            return true;
                        }};

                        if (!initializeAnalysis()) {{
                            return results;
                        }}

                        // Step 2: Component Detection and Classification
                        const analyzeComponents = () => {{
                            const components = [];
                            let componentId = 1;

                            // Discover Angular components using multiple methods
                            const componentElements = Array.from(document.querySelectorAll('*'))
                                .filter(el => {{
                                    // Check for Angular-specific attributes
                                    return Array.from(el.attributes).some(attr => 
                                        attr.name.startsWith('_nghost') || 
                                        attr.name.startsWith('_ngcontent') ||
                                        attr.name.startsWith('ng-reflect') ||
                                        attr.name.includes('ng-version')
                                    );
                                }})
                                .slice(0, {maxComponents});

                            componentElements.forEach((element, index) => {{
                                try {{
                                    const component = window.ng.getComponent?.(element);
                                    if (component) {{
                                        const componentInfo = {{
                                            id: componentId++,
                                            element: element,
                                            componentName: component.constructor.name,
                                            selector: element.tagName.toLowerCase(),
                                            changeDetectionStrategy: 'Default',
                                            hasOnPush: false,
                                            isStandalone: false,
                                            hasSignals: false,
                                            lifecycleHooks: {{}},
                                            changeDetectionMetrics: {{
                                                triggerCount: 0,
                                                averageDuration: 0,
                                                maxDuration: 0,
                                                executionHistory: []
                                            }},
                                            performance: {{
                                                renderTime: 0,
                                                domUpdates: 0,
                                                complexityScore: 0
                                            }},
                                            issues: []
                                        }};

                                        // Detect change detection strategy
                                        if (component.constructor.ɵcmp?.changeDetection === 0) {{
                                            componentInfo.changeDetectionStrategy = 'OnPush';
                                            componentInfo.hasOnPush = true;
                                            results.componentAnalysis.onPushComponents++;
                                        }} else {{
                                            results.componentAnalysis.defaultComponents++;
                                        }}

                                        // Check for standalone components
                                        if (component.constructor.ɵcmp?.standalone) {{
                                            componentInfo.isStandalone = true;
                                        }}

                                        // Detect signals (Angular 16+)
                                        try {{
                                            Object.getOwnPropertyNames(component).forEach(prop => {{
                                                const value = component[prop];
                                                if (value && typeof value === 'function' && 
                                                    (value.ɵIsSignal || 
                                                     (value.constructor && value.constructor.name === 'SignalImpl'))) {{
                                                    componentInfo.hasSignals = true;
                                                }}
                                            }});
                                        }} catch (e) {{
                                            // Skip property inspection errors
                                        }}

                                        // Detect lifecycle hooks that affect change detection
                                        const changeDetectionHooks = [
                                            'ngDoCheck', 'ngOnChanges', 'ngAfterViewChecked', 
                                            'ngAfterContentChecked', 'ngOnInit', 'ngAfterViewInit'
                                        ];

                                        changeDetectionHooks.forEach(hookName => {{
                                            if (typeof component[hookName] === 'function') {{
                                                componentInfo.lifecycleHooks[hookName] = {{
                                                    implemented: true,
                                                    executionCount: 0,
                                                    totalExecutionTime: 0,
                                                    averageExecutionTime: 0
                                                }};
                                            }}
                                        }});

                                        // Calculate complexity score
                                        const children = element.children.length;
                                        const depth = getElementDepth(element);
                                        const hasComplexBindings = Array.from(element.attributes)
                                            .some(attr => attr.name.startsWith('ng-') || attr.value.includes('{{'));
                                        
                                        componentInfo.performance.complexityScore = 
                                            children * 0.5 + depth * 0.3 + (hasComplexBindings ? 2 : 0);

                                        components.push(componentInfo);
                                    }}
                                }} catch (e) {{
                                    // Skip components that can't be analyzed
                                }}
                            }});

                            results.componentAnalysis.componentsAnalyzed = components.length;
                            return components;
                        }};

                        // Helper function to calculate element depth
                        const getElementDepth = (element) => {{
                            let depth = 0;
                            let parent = element.parentElement;
                            while (parent) {{
                                depth++;
                                parent = parent.parentElement;
                            }}
                            return depth;
                        }};

                        // Step 3: Change Detection Monitoring Setup
                        const setupChangeDetectionMonitoring = (components) => {{
                            const changeDetectionLog = [];
                            let cycleCounter = 0;

                            // Monitor Zone.js patches if available
                            if (window.Zone) {{
                                const originalRunTask = window.Zone.current.runTask;
                                if (originalRunTask) {{
                                    window.Zone.current.runTask = function(task, applyThis, applyArgs) {{
                                        const startTime = performance.now();
                                        const cycleId = ++cycleCounter;
                                        
                                        const result = originalRunTask.call(this, task, applyThis, applyArgs);
                                        
                                        const endTime = performance.now();
                                        const duration = endTime - startTime;
                                        
                                        changeDetectionLog.push({{
                                            cycleId: cycleId,
                                            startTime: startTime,
                                            duration: duration,
                                            taskType: task.type || 'unknown',
                                            taskSource: task.source || 'unknown',
                                            triggeredComponents: components.length, // Simplified
                                            timestamp: Date.now()
                                        }});
                                        
                                        return result;
                                    }};
                                }}
                            }}

                            // Setup component-level change detection monitoring
                            components.forEach(componentInfo => {{
                                const component = window.ng.getComponent(componentInfo.element);
                                if (!component) return;

                                // Monitor lifecycle hooks that indicate change detection
                                Object.keys(componentInfo.lifecycleHooks).forEach(hookName => {{
                                    if (component[hookName]) {{
                                        const originalMethod = component[hookName].bind(component);
                                        
                                        component[hookName] = function(...args) {{
                                            const startTime = performance.now();
                                            const hookStats = componentInfo.lifecycleHooks[hookName];
                                            
                                            try {{
                                                const result = originalMethod.apply(this, args);
                                                
                                                const endTime = performance.now();
                                                const executionTime = endTime - startTime;
                                                
                                                hookStats.executionCount++;
                                                hookStats.totalExecutionTime += executionTime;
                                                hookStats.averageExecutionTime = 
                                                    hookStats.totalExecutionTime / hookStats.executionCount;
                                                
                                                componentInfo.changeDetectionMetrics.triggerCount++;
                                                componentInfo.changeDetectionMetrics.averageDuration = 
                                                    (componentInfo.changeDetectionMetrics.averageDuration * 
                                                     (componentInfo.changeDetectionMetrics.triggerCount - 1) + executionTime) / 
                                                    componentInfo.changeDetectionMetrics.triggerCount;
                                                
                                                if (executionTime > componentInfo.changeDetectionMetrics.maxDuration) {{
                                                    componentInfo.changeDetectionMetrics.maxDuration = executionTime;
                                                }}
                                                
                                                componentInfo.changeDetectionMetrics.executionHistory.push({{
                                                    hookName: hookName,
                                                    executionTime: executionTime,
                                                    timestamp: Date.now(),
                                                    args: args.length
                                                }});
                                                
                                                return result;
                                            }} catch (error) {{
                                                componentInfo.issues.push({{
                                                    type: 'lifecycle_error',
                                                    hookName: hookName,
                                                    error: error.message,
                                                    timestamp: Date.now()
                                                }});
                                                throw error;
                                            }}
                                        }};
                                    }}
                                }});
                            }});

                            return {{ changeDetectionLog, components }};
                        }};

                        // Step 4: Real-time Performance Monitoring
                        const monitorPerformance = () => {{
                            const performanceData = {{
                                frameRates: [],
                                renderingDelays: [],
                                longTasks: []
                            }};

                            // Monitor frame rate using requestAnimationFrame
                            let frameCount = 0;
                            let lastTime = performance.now();
                            
                            const measureFrameRate = () => {{
                                frameCount++;
                                const currentTime = performance.now();
                                
                                if (currentTime - lastTime >= 1000) {{
                                    const fps = Math.round(frameCount * 1000 / (currentTime - lastTime));
                                    performanceData.frameRates.push({{
                                        fps: fps,
                                        timestamp: currentTime
                                    }});
                                    
                                    frameCount = 0;
                                    lastTime = currentTime;
                                }}
                                
                                requestAnimationFrame(measureFrameRate);
                            }};
                            
                            requestAnimationFrame(measureFrameRate);

                            // Monitor long tasks if PerformanceObserver is available
                            if (window.PerformanceObserver) {{
                                try {{
                                    const longTaskObserver = new PerformanceObserver((list) => {{
                                        for (const entry of list.getEntries()) {{
                                            if (entry.duration > 50) {{ // Tasks longer than 50ms
                                                performanceData.longTasks.push({{
                                                    duration: entry.duration,
                                                    startTime: entry.startTime,
                                                    name: entry.name,
                                                    entryType: entry.entryType
                                                }});
                                            }}
                                        }}
                                    }});
                                    
                                    longTaskObserver.observe({{ entryTypes: ['longtask'] }});
                                }} catch (e) {{
                                    console.warn('Long task monitoring not supported:', e.message);
                                }}
                            }}

                            return performanceData;
                        }};

                        // Step 5: Bottleneck Analysis and Classification
                        const analyzeBottlenecks = (components, changeDetectionLog, performanceData) => {{
                            const bottlenecks = {{
                                critical: [],
                                major: [],
                                minor: []
                            }};

                            // Analyze component-level bottlenecks
                            components.forEach(component => {{
                                const issues = [];

                                // Check for excessive change detection triggers
                                if (component.changeDetectionMetrics.triggerCount > 100) {{
                                    issues.push({{
                                        type: 'excessive_change_detection',
                                        severity: component.changeDetectionMetrics.triggerCount > 200 ? 'critical' : 'major',
                                        component: component.componentName,
                                        triggerCount: component.changeDetectionMetrics.triggerCount,
                                        averageDuration: component.changeDetectionMetrics.averageDuration,
                                        description: `Component triggered change detection ${{component.changeDetectionMetrics.triggerCount}} times`,
                                        recommendation: component.hasOnPush ? 
                                            'Review input changes and consider immutable data patterns' :
                                            'Implement OnPush change detection strategy',
                                        impact: 'High - Causes frequent unnecessary DOM checks'
                                    }});
                                }}

                                // Check for slow lifecycle hooks
                                Object.entries(component.lifecycleHooks).forEach(([hookName, hookData]) => {{
                                    if (hookData.averageExecutionTime > 10) {{
                                        issues.push({{
                                            type: 'slow_lifecycle_hook',
                                            severity: hookData.averageExecutionTime > 50 ? 'critical' : 'major',
                                            component: component.componentName,
                                            hookName: hookName,
                                            averageTime: hookData.averageExecutionTime,
                                            executionCount: hookData.executionCount,
                                            description: `${{hookName}} hook is slow (avg: ${{hookData.averageExecutionTime.toFixed(2)}}ms)`,
                                            recommendation: 'Optimize logic in lifecycle hook or move heavy operations to async methods',
                                            impact: 'Medium - Slows down component updates'
                                        }});
                                    }}
                                }});

                                // Check for ngDoCheck overuse
                                if (component.lifecycleHooks.ngDoCheck && 
                                    component.lifecycleHooks.ngDoCheck.executionCount > 50) {{
                                    issues.push({{
                                        type: 'excessive_ng_do_check',
                                        severity: 'major',
                                        component: component.componentName,
                                        executionCount: component.lifecycleHooks.ngDoCheck.executionCount,
                                        description: 'ngDoCheck called excessively - indicates change detection issues',
                                        recommendation: 'Review change detection strategy and consider OnPush with immutable data',
                                        impact: 'High - Causes performance degradation'
                                    }});
                                }}

                                // Check for complex components without OnPush
                                if (!component.hasOnPush && component.performance.complexityScore > 5) {{
                                    issues.push({{
                                        type: 'complex_component_without_onpush',
                                        severity: 'major',
                                        component: component.componentName,
                                        complexityScore: component.performance.complexityScore,
                                        description: 'Complex component using default change detection strategy',
                                        recommendation: 'Implement OnPush change detection strategy',
                                        impact: 'Medium - Unnecessary change detection cycles'
                                    }});
                                }}

                                // Classify issues by severity
                                issues.forEach(issue => {{
                                    switch (issue.severity) {{
                                        case 'critical':
                                            bottlenecks.critical.push(issue);
                                            break;
                                        case 'major':
                                            bottlenecks.major.push(issue);
                                            break;
                                        default:
                                            bottlenecks.minor.push(issue);
                                    }}
                                }});
                            }});

                            // Analyze global change detection patterns
                            if (changeDetectionLog.length > 0) {{
                                const averageCycleTime = changeDetectionLog.reduce((sum, cycle) => sum + cycle.duration, 0) / changeDetectionLog.length;
                                const slowCycles = changeDetectionLog.filter(cycle => cycle.duration > 16.67); // Slower than 60fps

                                if (averageCycleTime > 10) {{
                                    bottlenecks.major.push({{
                                        type: 'slow_change_detection_cycles',
                                        severity: 'major',
                                        averageTime: averageCycleTime,
                                        slowCycleCount: slowCycles.length,
                                        totalCycles: changeDetectionLog.length,
                                        description: `Change detection cycles are slow (avg: ${{averageCycleTime.toFixed(2)}}ms)`,
                                        recommendation: 'Optimize component change detection strategies and reduce component complexity',
                                        impact: 'High - Affects application responsiveness'
                                    }});
                                }}

                                if (changeDetectionLog.length / {durationSeconds} > 30) {{
                                    bottlenecks.major.push({{
                                        type: 'excessive_change_detection_frequency',
                                        severity: 'major',
                                        cyclesPerSecond: changeDetectionLog.length / {durationSeconds},
                                        description: 'Change detection triggered too frequently',
                                        recommendation: 'Review event handlers and async operations that trigger change detection',
                                        impact: 'High - Wastes CPU resources'
                                    }});
                                }}
                            }}

                            // Analyze performance data
                            if (performanceData.frameRates.length > 0) {{
                                const averageFps = performanceData.frameRates.reduce((sum, frame) => sum + frame.fps, 0) / performanceData.frameRates.length;
                                
                                if (averageFps < 30) {{
                                    bottlenecks.critical.push({{
                                        type: 'low_frame_rate',
                                        severity: 'critical',
                                        averageFps: averageFps,
                                        description: `Low frame rate detected (avg: ${{averageFps.toFixed(1)}} FPS)`,
                                        recommendation: 'Optimize change detection, reduce DOM manipulations, and consider virtual scrolling',
                                        impact: 'Critical - Poor user experience'
                                    }});
                                }}
                            }}

                            if (performanceData.longTasks.length > 0) {{
                                const totalLongTaskTime = performanceData.longTasks.reduce((sum, task) => sum + task.duration, 0);
                                
                                bottlenecks.major.push({{
                                    type: 'long_running_tasks',
                                    severity: 'major',
                                    taskCount: performanceData.longTasks.length,
                                    totalDuration: totalLongTaskTime,
                                    description: `${{performanceData.longTasks.length}} long-running tasks detected`,
                                    recommendation: 'Break down long tasks or run them outside Angular zone',
                                    impact: 'Medium - Blocks UI interactions'
                                }});
                            }}

                            return bottlenecks;
                        }};

                        // Step 6: Generate Optimization Recommendations
                        const generateRecommendations = (bottlenecks, componentAnalysis) => {{
                            const recommendations = [];

                            // Strategy recommendations based on component mix
                            if (componentAnalysis.defaultComponents > componentAnalysis.onPushComponents * 2) {{
                                recommendations.push({{
                                    priority: 'high',
                                    category: 'architecture',
                                    title: 'Implement OnPush Change Detection Strategy',
                                    description: `${{componentAnalysis.defaultComponents}} components using default change detection`,
                                    details: 'OnPush strategy can significantly reduce change detection cycles',
                                    implementation: [
                                        'Add ChangeDetectionStrategy.OnPush to component decorators',
                                        'Ensure immutable data patterns',
                                        'Use async pipe for observables',
                                        'Call ChangeDetectorRef.markForCheck() when needed'
                                    ],
                                    estimatedImpact: 'High - 30-60% performance improvement'
                                }});
                            }}

                            // Memory leak recommendations
                            if (bottlenecks.major.some(b => b.type === 'excessive_change_detection')) {{
                                recommendations.push({{
                                    priority: 'high',
                                    category: 'memory',
                                    title: 'Fix Memory Leaks and Subscription Management',
                                    description: 'Excessive change detection often indicates memory leaks',
                                    details: 'Unsubscribed observables and event listeners cause unnecessary updates',
                                    implementation: [
                                        'Implement ngOnDestroy and unsubscribe from observables',
                                        'Use takeUntil pattern for subscription management',
                                        'Remove event listeners in ngOnDestroy',
                                        'Use OnPush with async pipe to reduce manual subscriptions'
                                    ],
                                    estimatedImpact: 'High - Prevents memory leaks and reduces CPU usage'
                                }});
                            }}

                            // Zone.js optimization
                            if (window.Zone && bottlenecks.major.some(b => b.type === 'excessive_change_detection_frequency')) {{
                                recommendations.push({{
                                    priority: 'medium',
                                    category: 'zone',
                                    title: 'Optimize Zone.js Usage',
                                    description: 'Run non-UI operations outside Angular zone',
                                    details: 'Heavy computations and external library operations should not trigger change detection',
                                    implementation: [
                                        'Use NgZone.runOutsideAngular() for heavy computations',
                                        'Manually trigger change detection only when needed',
                                        'Consider zoneless change detection for Angular 18+',
                                        'Debounce frequent events'
                                    ],
                                    estimatedImpact: 'Medium - Reduces unnecessary change detection cycles'
                                }});
                            }}

                            // Performance monitoring
                            recommendations.push({{
                                priority: 'low',
                                category: 'monitoring',
                                title: 'Implement Performance Monitoring',
                                description: 'Set up continuous performance monitoring',
                                details: 'Regular monitoring helps identify performance regressions early',
                                implementation: [
                                    'Use Angular DevTools for development monitoring',
                                    'Implement performance budgets in CI/CD',
                                    'Set up user experience monitoring in production',
                                    'Regular change detection performance audits'
                                ],
                                estimatedImpact: 'Low - Preventive measure for long-term performance'
                            }});

                            return recommendations;
                        }};

                        // Execute Analysis
                        results.analysisStarted = true;
                        const components = analyzeComponents();
                        const {{ changeDetectionLog, components: monitoredComponents }} = setupChangeDetectionMonitoring(components);
                        const performanceData = monitorPerformance();

                        // Set completion timeout
                        setTimeout(() => {{
                            results.endTime = Date.now();
                            results.actualDuration = (results.endTime - results.startTime) / 1000;

                            // Final analysis
                            const bottlenecks = analyzeBottlenecks(monitoredComponents, changeDetectionLog, performanceData);
                            const recommendations = generateRecommendations(bottlenecks, results.componentAnalysis);

                            // Compile final results
                            results.bottlenecks = bottlenecks;
                            results.bottlenecks.summary = {{
                                totalBottlenecks: bottlenecks.critical.length + bottlenecks.major.length + bottlenecks.minor.length,
                                criticalCount: bottlenecks.critical.length,
                                majorCount: bottlenecks.major.length,
                                minorCount: bottlenecks.minor.length
                            }};

                            results.optimizationRecommendations = recommendations;

                            // Calculate metrics
                            if (changeDetectionLog.length > 0) {{
                                results.changeDetectionMetrics.totalCycles = changeDetectionLog.length;
                                results.changeDetectionMetrics.averageCycleTime = 
                                    changeDetectionLog.reduce((sum, cycle) => sum + cycle.duration, 0) / changeDetectionLog.length;
                                results.changeDetectionMetrics.slowestCycle = 
                                    Math.max(...changeDetectionLog.map(cycle => cycle.duration));
                                results.changeDetectionMetrics.fastestCycle = 
                                    Math.min(...changeDetectionLog.map(cycle => cycle.duration));
                                results.changeDetectionMetrics.cyclesPerSecond = changeDetectionLog.length / results.actualDuration;
                                results.changeDetectionMetrics.excessiveCycles = 
                                    changeDetectionLog.filter(cycle => cycle.duration > 16.67).length;
                            }}

                            // Performance impact assessment
                            if (performanceData.frameRates.length > 0) {{
                                results.performanceImpact.fpsMeasurement = 
                                    performanceData.frameRates.reduce((sum, frame) => sum + frame.fps, 0) / performanceData.frameRates.length;
                            }}

                            results.performanceImpact.userExperienceScore = calculateUserExperienceScore(results);
                            results.performanceImpact.estimatedImpact = assessPerformanceImpact(results);

                            // Store results for retrieval
                            window.__angularChangeDetectionAnalysisResults = results;
                        }}, {durationSeconds * 1000});

                        return {{
                            message: `Starting change detection bottleneck analysis for ${{components.length}} components over {durationSeconds} seconds...`,
                            componentsFound: components.length,
                            analysisStarted: true
                        }};
                    }};

                    // Helper functions
                    const calculateUserExperienceScore = (results) => {{
                        let score = 100;
                        
                        // Deduct points for issues
                        score -= results.bottlenecks.critical.length * 20;
                        score -= results.bottlenecks.major.length * 10;
                        score -= results.bottlenecks.minor.length * 5;
                        
                        // Deduct for poor frame rate
                        if (results.performanceImpact.fpsMeasurement < 30) {{
                            score -= 30;
                        }} else if (results.performanceImpact.fpsMeasurement < 45) {{
                            score -= 15;
                        }}
                        
                        // Deduct for excessive change detection
                        if (results.changeDetectionMetrics.cyclesPerSecond > 30) {{
                            score -= 20;
                        }}
                        
                        return Math.max(0, score);
                    }};

                    const assessPerformanceImpact = (results) => {{
                        const criticalIssues = results.bottlenecks.critical.length;
                        const majorIssues = results.bottlenecks.major.length;
                        const fps = results.performanceImpact.fpsMeasurement;
                        
                        if (criticalIssues > 0 || fps < 20) {{
                            return 'severe';
                        }} else if (majorIssues > 2 || fps < 40) {{
                            return 'high';
                        }} else if (majorIssues > 0 || fps < 50) {{
                            return 'medium';
                        }} else {{
                            return 'low';
                        }}
                    }};

                    return detectChangeDetectionBottlenecks();
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            
            // Wait for analysis to complete
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds + 2));
            
            // Retrieve final results
            try
            {
                var finalResultsJs = @"
                    (() => {
                        if (window.__angularChangeDetectionAnalysisResults) {
                            return window.__angularChangeDetectionAnalysisResults;
                        }
                        return { error: 'Change detection analysis results not available' };
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
            return $"Failed to detect change detection bottlenecks: {ex.Message}";
        }
    }
}
