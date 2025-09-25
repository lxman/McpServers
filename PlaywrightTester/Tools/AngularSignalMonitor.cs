using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// ANG-002: Signal Monitoring Implementation 
/// ANG-006: Signal Dependency Analysis Implementation
/// Implements monitor_signal_updates and analyze_signal_dependencies functions for comprehensive signal tracking and dependency analysis
/// </summary>
[McpServerToolType]
public class AngularSignalMonitor(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Monitor Angular signals in real-time, tracking signal changes and dependencies - ANG-002 Implementation")]
    public async Task<string> MonitorSignalUpdates(
        [Description("Duration in seconds to monitor signals")] int durationSeconds = 30,
        [Description("Maximum number of signals to track simultaneously")] int maxSignals = 50,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    // ANG-002: Signal Monitoring Implementation
                    const monitorSignalUpdates = () => {{
                        const results = {{
                            angularDetected: false,
                            signalsSupported: false,
                            monitoringStarted: false,
                            monitoringDuration: {durationSeconds},
                            maxSignals: {maxSignals},
                            startTime: Date.now(),
                            endTime: null,
                            signals: {{
                                discovered: [],
                                monitored: [],
                                updates: [],
                                dependencies: []
                            }},
                            statistics: {{
                                totalSignals: 0,
                                totalUpdates: 0,
                                averageUpdateFrequency: 0,
                                mostActiveSignal: null,
                                leastActiveSignal: null,
                                updatesByType: {{
                                    programmatic: 0,
                                    userInteraction: 0,
                                    computed: 0
                                }}
                            }},
                            analysis: {{
                                circularDependencies: [],
                                performanceIssues: [],
                                recommendations: [],
                                dependencyGraph: []
                            }},
                            timeline: []
                        }};

                        // Step 1: Check for Angular and Signals support
                        const checkAngularSignals = () => {{
                            if (!window.ng) {{
                                results.error = 'Angular not detected or not in development mode';
                                return false;
                            }}

                            results.angularDetected = true;

                            // Check for Signals API (Angular 16+)
                            const hasSignalsAPI = !!(window.ng.signal || 
                                                   (typeof window.signal === 'function') ||
                                                   window.ng.computed ||
                                                   window.ng.effect);

                            if (!hasSignalsAPI) {{
                                results.error = 'Angular Signals API not detected. Requires Angular 16+ with Signals enabled.';
                                return false;
                            }}

                            results.signalsSupported = true;
                            return true;
                        }};

                        if (!checkAngularSignals()) {{
                            return results;
                        }}

                        // Step 2: Discover existing signals in components
                        const discoverSignals = () => {{
                            const discoveredSignals = [];
                            
                            // Method 1: Scan component instances for signals
                            if (window.ng.getComponent) {{
                                const componentElements = Array.from(document.querySelectorAll('*'))
                                    .filter(el => Array.from(el.attributes).some(attr => 
                                        attr.name.startsWith('_nghost') || attr.name.startsWith('ng-reflect')))
                                    .slice(0, 100); // Limit for performance

                                componentElements.forEach((el, index) => {{
                                    try {{
                                        const component = window.ng.getComponent(el);
                                        if (component) {{
                                            // Scan component properties for signals
                                            Object.getOwnPropertyNames(component).forEach(prop => {{
                                                try {{
                                                    const value = component[prop];
                                                    // Detect Angular signals by their characteristics
                                                    if (value && typeof value === 'function' && 
                                                        (value.ƵIsSignal || 
                                                         (value.constructor && value.constructor.name === 'SignalImpl') ||
                                                         (typeof value.set === 'function' && typeof value.update === 'function'))) {{
                                                        
                                                        const signalInfo = {{
                                                            id: `${{el.tagName.toLowerCase()}}-${{prop}}-${{index}}`,
                                                            componentName: component.constructor.name,
                                                            propertyName: prop,
                                                            element: el.tagName.toLowerCase(),
                                                            elementIndex: index,
                                                            currentValue: null,
                                                            previousValue: null,
                                                            type: 'writable',
                                                            updateCount: 0,
                                                            lastUpdated: null,
                                                            dependencies: [],
                                                            dependents: [],
                                                            originalSignal: value
                                                        }};

                                                        // Try to get current value safely
                                                        try {{
                                                            signalInfo.currentValue = JSON.stringify(value());
                                                        }} catch (e) {{
                                                            signalInfo.currentValue = '[Unable to read]';
                                                        }}

                                                        // Check if it's a computed signal
                                                        if (value.constructor && value.constructor.name === 'ComputedImpl') {{
                                                            signalInfo.type = 'computed';
                                                        }}

                                                        // Check if it's an effect
                                                        if (value.constructor && value.constructor.name === 'EffectImpl') {{
                                                            signalInfo.type = 'effect';
                                                        }}

                                                        discoveredSignals.push(signalInfo);
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
                            }}

                            // Method 2: Look for signals in global scope (if exposed for testing)
                            if (window.signals) {{
                                Object.keys(window.signals).forEach(key => {{
                                    const signal = window.signals[key];
                                    if (signal && typeof signal === 'function' && signal.ƵIsSignal) {{
                                        discoveredSignals.push({{
                                            id: `global-${{key}}`,
                                            componentName: 'Global',
                                            propertyName: key,
                                            element: 'global',
                                            elementIndex: -1,
                                            currentValue: JSON.stringify(signal()),
                                            previousValue: null,
                                            type: 'global',
                                            updateCount: 0,
                                            lastUpdated: null,
                                            dependencies: [],
                                            dependents: [],
                                            originalSignal: signal
                                        }});
                                    }}
                                }});
                            }}

                            // Limit to maxSignals to prevent performance issues
                            results.signals.discovered = discoveredSignals.slice(0, {maxSignals});
                            results.statistics.totalSignals = results.signals.discovered.length;
                            
                            return results.signals.discovered;
                        }};

                        // Step 3: Set up signal monitoring with proxies
                        const setupSignalMonitoring = (discoveredSignals) => {{
                            const monitoredSignals = [];
                            const updateLog = [];

                            discoveredSignals.forEach(signalInfo => {{
                                try {{
                                    const originalSignal = signalInfo.originalSignal;
                                    
                                    // For writable signals, wrap set/update methods
                                    if (signalInfo.type === 'writable' && originalSignal.set && originalSignal.update) {{
                                        const originalSet = originalSignal.set.bind(originalSignal);
                                        const originalUpdate = originalSignal.update.bind(originalSignal);

                                        // Wrap set method
                                        originalSignal.set = function(newValue) {{
                                            const timestamp = Date.now();
                                            const oldValue = signalInfo.currentValue;
                                            
                                            const updateEvent = {{
                                                signalId: signalInfo.id,
                                                timestamp: timestamp,
                                                type: 'set',
                                                oldValue: oldValue,
                                                newValue: JSON.stringify(newValue),
                                                source: 'programmatic',
                                                stackTrace: new Error().stack?.split('\\n').slice(1, 4).join('\\n') || 'unavailable'
                                            }};

                                            updateLog.push(updateEvent);
                                            results.timeline.push(updateEvent);
                                            
                                            signalInfo.previousValue = signalInfo.currentValue;
                                            signalInfo.currentValue = JSON.stringify(newValue);
                                            signalInfo.updateCount++;
                                            signalInfo.lastUpdated = timestamp;
                                            
                                            results.statistics.totalUpdates++;
                                            results.statistics.updatesByType.programmatic++;

                                            return originalSet(newValue);
                                        }};

                                        // Wrap update method
                                        originalSignal.update = function(updateFn) {{
                                            const timestamp = Date.now();
                                            const oldValue = signalInfo.currentValue;
                                            
                                            const result = originalUpdate(updateFn);
                                            
                                            const updateEvent = {{
                                                signalId: signalInfo.id,
                                                timestamp: timestamp,
                                                type: 'update',
                                                oldValue: oldValue,
                                                newValue: signalInfo.currentValue,
                                                source: 'programmatic',
                                                stackTrace: new Error().stack?.split('\\n').slice(1, 4).join('\\n') || 'unavailable'
                                            }};

                                            updateLog.push(updateEvent);
                                            results.timeline.push(updateEvent);
                                            
                                            signalInfo.previousValue = oldValue;
                                            signalInfo.updateCount++;
                                            signalInfo.lastUpdated = timestamp;
                                            
                                            results.statistics.totalUpdates++;
                                            results.statistics.updatesByType.programmatic++;

                                            return result;
                                        }};
                                    }}

                                    monitoredSignals.push(signalInfo);
                                }} catch (e) {{
                                    console.warn('Failed to monitor signal:', signalInfo.id, e);
                                }}
                            }});

                            results.signals.monitored = monitoredSignals;
                            results.signals.updates = updateLog;
                            return monitoredSignals;
                        }};

                        // Step 4: Analyze signal dependencies
                        const analyzeSignalDependencies = (monitoredSignals) => {{
                            const dependencies = [];
                            const dependencyGraph = [];

                            // Basic dependency detection (this is simplified - real analysis would need deeper inspection)
                            monitoredSignals.forEach(signal => {{
                                if (signal.type === 'computed') {{
                                    // Computed signals depend on other signals
                                    // This is a heuristic approach since we can't easily inspect computed signal dependencies
                                    const possibleDependencies = monitoredSignals
                                        .filter(s => s.id !== signal.id && s.componentName === signal.componentName)
                                        .map(s => s.id);
                                    
                                    if (possibleDependencies.length > 0) {{
                                        dependencies.push({{
                                            signalId: signal.id,
                                            dependsOn: possibleDependencies.slice(0, 3), // Limit for performance
                                            type: 'computed'
                                        }});

                                        dependencyGraph.push({{
                                            from: signal.id,
                                            to: possibleDependencies,
                                            relationship: 'computed-from'
                                        }});
                                    }}
                                }}
                            }});

                            results.signals.dependencies = dependencies;
                            results.analysis.dependencyGraph = dependencyGraph;

                            // Check for potential circular dependencies
                            const circularDeps = [];
                            dependencies.forEach(dep => {{
                                dep.dependsOn.forEach(depId => {{
                                    const reverseDep = dependencies.find(d => 
                                        d.signalId === depId && d.dependsOn.includes(dep.signalId));
                                    if (reverseDep) {{
                                        circularDeps.push({{
                                            signals: [dep.signalId, depId],
                                            severity: 'warning',
                                            message: 'Potential circular dependency detected'
                                        }});
                                    }}
                                }});
                            }});

                            results.analysis.circularDependencies = circularDeps;
                        }};

                        // Step 5: Performance analysis and recommendations
                        const generateRecommendations = (monitoredSignals) => {{
                            const recommendations = [];

                            // Check for frequently updated signals
                            const highUpdateSignals = monitoredSignals.filter(s => s.updateCount > 10);
                            if (highUpdateSignals.length > 0) {{
                                recommendations.push({{
                                    type: 'performance',
                                    severity: 'medium',
                                    message: `${{highUpdateSignals.length}} signals with high update frequency detected`,
                                    details: 'Consider batching updates or using computed signals to reduce change detection cycles',
                                    affectedSignals: highUpdateSignals.map(s => s.id)
                                }});
                            }}

                            // Check for signals with complex values
                            const complexValueSignals = monitoredSignals.filter(s => 
                                s.currentValue && s.currentValue.length > 1000);
                            if (complexValueSignals.length > 0) {{
                                recommendations.push({{
                                    type: 'memory',
                                    severity: 'low',
                                    message: `${{complexValueSignals.length}} signals storing large values`,
                                    details: 'Consider normalizing data or using references to reduce memory usage',
                                    affectedSignals: complexValueSignals.map(s => s.id)
                                }});
                            }}

                            // Check for unused signals (no updates during monitoring)
                            const unusedSignals = monitoredSignals.filter(s => s.updateCount === 0);
                            if (unusedSignals.length > 0) {{
                                recommendations.push({{
                                    type: 'cleanup',
                                    severity: 'low',
                                    message: `${{unusedSignals.length}} signals with no updates during monitoring`,
                                    details: 'Consider removing unused signals or investigating if they should be active',
                                    affectedSignals: unusedSignals.map(s => s.id)
                                }});
                            }}

                            results.analysis.recommendations = recommendations;
                        }};

                        // Step 6: Execute monitoring
                        const executeMonitoring = () => {{
                            results.monitoringStarted = true;
                            
                            const discoveredSignals = discoverSignals();
                            if (discoveredSignals.length === 0) {{
                                results.error = 'No signals detected in the application';
                                return results;
                            }}

                            const monitoredSignals = setupSignalMonitoring(discoveredSignals);
                            analyzeSignalDependencies(monitoredSignals);

                            // Set up completion timer
                            setTimeout(() => {{
                                results.endTime = Date.now();
                                results.monitoringDuration = (results.endTime - results.startTime) / 1000;

                                // Calculate statistics
                                if (results.statistics.totalUpdates > 0) {{
                                    results.statistics.averageUpdateFrequency = 
                                        results.statistics.totalUpdates / results.monitoringDuration;
                                }}

                                const signalUpdateCounts = monitoredSignals.map(s => ({{
                                    id: s.id,
                                    count: s.updateCount
                                }}));

                                if (signalUpdateCounts.length > 0) {{
                                    const maxUpdates = Math.max(...signalUpdateCounts.map(s => s.count));
                                    const minUpdates = Math.min(...signalUpdateCounts.map(s => s.count));

                                    results.statistics.mostActiveSignal = signalUpdateCounts.find(s => s.count === maxUpdates);
                                    results.statistics.leastActiveSignal = signalUpdateCounts.find(s => s.count === minUpdates);
                                }}

                                generateRecommendations(monitoredSignals);

                                // Store results globally for retrieval
                                window.__angularSignalMonitoringResults = results;
                            }}, {durationSeconds * 1000});

                            return results;
                        }};

                        return executeMonitoring();
                    }};

                    return monitorSignalUpdates();
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
                        if (window.__angularSignalMonitoringResults) {
                            return window.__angularSignalMonitoringResults;
                        }
                        return { error: 'Monitoring results not available' };
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
            return $"Failed to monitor signal updates: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Analyze signal dependencies and detect circular dependencies - ANG-006 Implementation")]
    public async Task<string> AnalyzeSignalDependencies(
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = @"
                (() => {
                    // ANG-006: Signal Dependency Analysis Implementation
                    const analyzeSignalDependencies = () => {
                        const results = {
                            angularDetected: false,
                            signalsSupported: false,
                            analysisStartTime: Date.now(),
                            signals: {
                                discovered: [],
                                byType: {
                                    writable: [],
                                    computed: [],
                                    effect: []
                                },
                                byComponent: {}
                            },
                            dependencyGraph: {
                                nodes: [],
                                edges: [],
                                components: [],
                                layers: []
                            },
                            circularDependencies: [],
                            analysis: {
                                totalSignals: 0,
                                dependentSignals: 0,
                                independentSignals: 0,
                                maxDependencyDepth: 0,
                                complexityScore: 0,
                                isolationScore: 0,
                                recommendations: [],
                                performanceRisks: [],
                                architectureInsights: []
                            },
                            qualityMetrics: {
                                signalToComponentRatio: 0,
                                computedToWritableRatio: 0,
                                dependencyComplexity: 0,
                                circularDependencyCount: 0,
                                unusedSignalCount: 0
                            }
                        };

                        // Step 1: Environment Detection and Validation
                        const detectAngularEnvironment = () => {
                            if (!window.ng) {
                                results.error = 'Angular not detected or not in development mode';
                                return false;
                            }

                            results.angularDetected = true;

                            // Check for Signals API (Angular 16+)
                            const hasSignalsAPI = !!(
                                window.ng.signal || 
                                (typeof window.signal === 'function') ||
                                window.ng.computed ||
                                window.ng.effect ||
                                window.signal
                            );

                            if (!hasSignalsAPI) {
                                results.error = 'Angular Signals API not detected. Requires Angular 16+ with Signals enabled.';
                                return false;
                            }

                            results.signalsSupported = true;

                            // Detect Angular version if possible
                            try {
                                if (window.ng.version) {
                                    results.angularVersion = window.ng.version.full;
                                } else if (window.ng.VERSION) {
                                    results.angularVersion = window.ng.VERSION.full;
                                }
                            } catch (e) {
                                // Version detection is optional
                            }

                            return true;
                        };

                        if (!detectAngularEnvironment()) {
                            return results;
                        }

                        // Step 2: Advanced Signal Discovery
                        const discoverSignalsAdvanced = () => {
                            const discoveredSignals = [];
                            const componentMap = new Map();

                            // Method 1: Comprehensive component scanning
                            if (window.ng.getComponent) {
                                const componentElements = Array.from(document.querySelectorAll('*'))
                                    .filter(el => {
                                        // More robust Angular element detection
                                        return Array.from(el.attributes).some(attr => 
                                            attr.name.startsWith('_nghost') || 
                                            attr.name.startsWith('_ngcontent') ||
                                            attr.name.startsWith('ng-reflect') ||
                                            attr.name.includes('ng-')
                                        );
                                    })
                                    .slice(0, 200); // Increased limit for better coverage

                                componentElements.forEach((el, index) => {
                                    try {
                                        const component = window.ng.getComponent(el);
                                        if (component) {
                                            const componentName = component.constructor.name;
                                            
                                            if (!componentMap.has(componentName)) {
                                                componentMap.set(componentName, {
                                                    name: componentName,
                                                    elements: [],
                                                    signals: []
                                                });
                                            }

                                            const componentInfo = componentMap.get(componentName);
                                            componentInfo.elements.push(el);

                                            // Enhanced signal detection
                                            const signalsInComponent = scanComponentForSignals(component, componentName, el, index);
                                            discoveredSignals.push(...signalsInComponent);
                                            componentInfo.signals.push(...signalsInComponent);
                                        }
                                    } catch (e) {
                                        // Skip elements without components
                                    }
                                });
                            }

                            // Method 2: Global signal detection
                            if (window.signals) {
                                Object.keys(window.signals).forEach(key => {
                                    const signal = window.signals[key];
                                    if (isSignalLike(signal)) {
                                        discoveredSignals.push(createSignalInfo(signal, 'Global', key, null, -1, 'global'));
                                    }
                                });
                            }

                            // Method 3: DevTools signal detection (if available)
                            if (window.ng.getDebugInfo && window.ng.getDebugInfo.signals) {
                                try {
                                    const debugSignals = window.ng.getDebugInfo.signals();
                                    debugSignals.forEach(signal => {
                                        if (!discoveredSignals.find(s => s.id === signal.id)) {
                                            discoveredSignals.push(signal);
                                        }
                                    });
                                } catch (e) {
                                    // Debug signals not available
                                }
                            }

                            return { signals: discoveredSignals, components: Array.from(componentMap.values()) };
                        };

                        // Helper function to scan component for signals
                        const scanComponentForSignals = (component, componentName, element, elementIndex) => {
                            const signals = [];
                            
                            // Get all property descriptors including inherited ones
                            const getAllProperties = (obj) => {
                                const props = new Set();
                                let current = obj;
                                
                                while (current && current !== Object.prototype) {
                                    Object.getOwnPropertyNames(current).forEach(prop => props.add(prop));
                                    current = Object.getPrototypeOf(current);
                                }
                                
                                return Array.from(props);
                            };

                            getAllProperties(component).forEach(prop => {
                                try {
                                    const value = component[prop];
                                    
                                    if (isSignalLike(value)) {
                                        const signalInfo = createSignalInfo(value, componentName, prop, element, elementIndex);
                                        signals.push(signalInfo);
                                    }
                                } catch (e) {
                                    // Skip properties that can't be accessed
                                }
                            });

                            return signals;
                        };

                        // Enhanced signal type detection
                        const isSignalLike = (value) => {
                            if (!value || typeof value !== 'function') return false;

                            // Multiple detection strategies
                            return !!(
                                value.ƵIsSignal ||                                           // Angular 16+ signal marker
                                (value.constructor && value.constructor.name === 'SignalImpl') ||  // WritableSignal
                                (value.constructor && value.constructor.name === 'ComputedImpl') || // ComputedSignal
                                (value.constructor && value.constructor.name === 'EffectImpl') ||   // Effect
                                (typeof value.set === 'function' && typeof value.update === 'function') || // Writable signal interface
                                (typeof value.destroy === 'function' && value.constructor?.name?.includes('Effect')) || // Effect interface
                                (value.toString && value.toString().includes('[object Signal]')) // toString detection
                            );
                        };

                        // Create comprehensive signal information
                        const createSignalInfo = (signal, componentName, propertyName, element, elementIndex, scope = 'component') => {
                            const signalInfo = {
                                id: `${componentName}-${propertyName}-${elementIndex}`,
                                name: propertyName,
                                componentName: componentName,
                                element: element ? element.tagName.toLowerCase() : scope,
                                elementIndex: elementIndex,
                                scope: scope,
                                type: determineSignalType(signal),
                                currentValue: null,
                                dependencies: [],
                                dependents: [],
                                isReadonly: false,
                                metadata: {
                                    constructorName: signal.constructor?.name,
                                    hasSetMethod: typeof signal.set === 'function',
                                    hasUpdateMethod: typeof signal.update === 'function',
                                    hasDestroyMethod: typeof signal.destroy === 'function',
                                    isAsync: false
                                },
                                performance: {
                                    creationTime: Date.now(),
                                    updateCount: 0,
                                    lastAccessed: null,
                                    computationTime: 0
                                },
                                originalSignal: signal
                            };

                            // Try to get current value safely
                            try {
                                const value = signal();
                                signalInfo.currentValue = typeof value === 'object' ? 
                                    JSON.stringify(value).substring(0, 1000) : // Limit large objects
                                    String(value);
                                signalInfo.performance.lastAccessed = Date.now();
                            } catch (e) {
                                signalInfo.currentValue = '[Unable to read]';
                                signalInfo.error = e.message;
                            }

                            // Check if signal is readonly
                            signalInfo.isReadonly = !signalInfo.metadata.hasSetMethod && !signalInfo.metadata.hasUpdateMethod;

                            return signalInfo;
                        };

                        // Determine signal type with enhanced detection
                        const determineSignalType = (signal) => {
                            if (!signal.constructor) return 'unknown';

                            const constructorName = signal.constructor.name;
                            
                            switch (constructorName) {
                                case 'SignalImpl':
                                case 'WritableSignal':
                                    return 'writable';
                                case 'ComputedImpl':
                                case 'ComputedSignal':
                                    return 'computed';
                                case 'EffectImpl':
                                case 'Effect':
                                    return 'effect';
                                default:
                                    // Fallback detection based on methods
                                    if (typeof signal.set === 'function' && typeof signal.update === 'function') {
                                        return 'writable';
                                    } else if (typeof signal.destroy === 'function') {
                                        return 'effect';
                                    } else if (signal.toString().includes('computed')) {
                                        return 'computed';
                                    }
                                    return 'unknown';
                            }
                        };

                        // Step 3: Comprehensive Dependency Graph Generation
                        const buildDependencyGraph = (signals, components) => {
                            const nodes = [];
                            const edges = [];
                            const dependencyMap = new Map();

                            // Create nodes for all signals
                            signals.forEach(signal => {
                                nodes.push({
                                    id: signal.id,
                                    name: signal.name,
                                    type: signal.type,
                                    component: signal.componentName,
                                    scope: signal.scope,
                                    value: signal.currentValue,
                                    complexity: calculateSignalComplexity(signal),
                                    metadata: signal.metadata
                                });
                            });

                            // Analyze dependencies using multiple strategies
                            
                            // Strategy 1: Heuristic analysis for computed signals
                            const computedSignals = signals.filter(s => s.type === 'computed');
                            const writableSignals = signals.filter(s => s.type === 'writable');

                            computedSignals.forEach(computed => {
                                // Find potential dependencies within same component
                                const sameLevelSignals = writableSignals.filter(w => 
                                    w.componentName === computed.componentName ||
                                    isLikelyDependency(computed, w)
                                );

                                sameLevelSignals.forEach(dependency => {
                                    edges.push({
                                        from: dependency.id,
                                        to: computed.id,
                                        type: 'computed-dependency',
                                        strength: calculateDependencyStrength(dependency, computed),
                                        component: computed.componentName
                                    });

                                    // Update signal dependency info
                                    computed.dependencies.push({
                                        signalId: dependency.id,
                                        type: 'computed-dependency',
                                        strength: 'medium'
                                    });

                                    dependency.dependents.push({
                                        signalId: computed.id,
                                        type: 'computed-dependency',
                                        strength: 'medium'
                                    });
                                });
                            });

                            // Strategy 2: Cross-component dependency analysis
                            analyzeCrossComponentDependencies(signals, edges);

                            // Strategy 3: Effect dependency analysis
                            const effectSignals = signals.filter(s => s.type === 'effect');
                            effectSignals.forEach(effect => {
                                // Effects typically depend on multiple signals
                                const possibleDependencies = signals.filter(s => 
                                    s.id !== effect.id && 
                                    (s.componentName === effect.componentName || s.scope === 'global')
                                );

                                possibleDependencies.forEach(dep => {
                                    edges.push({
                                        from: dep.id,
                                        to: effect.id,
                                        type: 'effect-dependency',
                                        strength: 'low', // Effects have weak coupling
                                        component: effect.componentName
                                    });
                                });
                            });

                            return { nodes, edges };
                        };

                        // Helper functions for dependency analysis
                        const calculateSignalComplexity = (signal) => {
                            let complexity = 1;
                            
                            // Add complexity based on value size
                            if (signal.currentValue && typeof signal.currentValue === 'string') {
                                complexity += Math.min(signal.currentValue.length / 100, 5);
                            }
                            
                            // Add complexity based on type
                            switch (signal.type) {
                                case 'computed': complexity += 2; break;
                                case 'effect': complexity += 1; break;
                                case 'writable': complexity += 0; break;
                            }
                            
                            return Math.round(complexity * 10) / 10;
                        };

                        const isLikelyDependency = (computed, potential) => {
                            // Simple heuristics for dependency detection
                            const nameMatch = computed.name.toLowerCase().includes(potential.name.toLowerCase()) ||
                                            potential.name.toLowerCase().includes(computed.name.toLowerCase());
                            
                            const scopeMatch = computed.componentName === potential.componentName;
                            
                            return nameMatch || scopeMatch;
                        };

                        const calculateDependencyStrength = (from, to) => {
                            // Calculate strength based on various factors
                            let strength = 0.5; // Base strength
                            
                            if (from.componentName === to.componentName) strength += 0.3;
                            if (to.type === 'computed') strength += 0.2;
                            
                            return Math.min(strength, 1.0);
                        };

                        const analyzeCrossComponentDependencies = (signals, edges) => {
                            // Group signals by component
                            const componentGroups = {};
                            signals.forEach(signal => {
                                if (!componentGroups[signal.componentName]) {
                                    componentGroups[signal.componentName] = [];
                                }
                                componentGroups[signal.componentName].push(signal);
                            });

                            // Look for cross-component patterns
                            Object.keys(componentGroups).forEach(componentA => {
                                Object.keys(componentGroups).forEach(componentB => {
                                    if (componentA !== componentB) {
                                        // Analyze potential cross-component dependencies
                                        const signalsA = componentGroups[componentA];
                                        const signalsB = componentGroups[componentB];

                                        signalsA.forEach(signalA => {
                                            signalsB.forEach(signalB => {
                                                if (isLikelyCrossComponentDependency(signalA, signalB)) {
                                                    edges.push({
                                                        from: signalA.id,
                                                        to: signalB.id,
                                                        type: 'cross-component',
                                                        strength: 'low',
                                                        components: [componentA, componentB]
                                                    });
                                                }
                                            });
                                        });
                                    }
                                });
                            });
                        };

                        const isLikelyCrossComponentDependency = (signalA, signalB) => {
                            // Heuristics for cross-component dependencies
                            const nameSimiliarity = calculateNameSimilarity(signalA.name, signalB.name);
                            return nameSimiliarity > 0.6;
                        };

                        const calculateNameSimilarity = (name1, name2) => {
                            // Simple string similarity calculation
                            const longer = name1.length > name2.length ? name1 : name2;
                            const shorter = name1.length > name2.length ? name2 : name1;
                            
                            if (longer.length === 0) return 1.0;
                            
                            const distance = levenshteinDistance(longer, shorter);
                            return (longer.length - distance) / longer.length;
                        };

                        const levenshteinDistance = (str1, str2) => {
                            const matrix = [];
                            for (let i = 0; i <= str2.length; i++) {
                                matrix[i] = [i];
                            }
                            for (let j = 0; j <= str1.length; j++) {
                                matrix[0][j] = j;
                            }
                            for (let i = 1; i <= str2.length; i++) {
                                for (let j = 1; j <= str1.length; j++) {
                                    if (str2.charAt(i - 1) === str1.charAt(j - 1)) {
                                        matrix[i][j] = matrix[i - 1][j - 1];
                                    } else {
                                        matrix[i][j] = Math.min(
                                            matrix[i - 1][j - 1] + 1,
                                            matrix[i][j - 1] + 1,
                                            matrix[i - 1][j] + 1
                                        );
                                    }
                                }
                            }
                            return matrix[str2.length][str1.length];
                        };

                        // Step 4: Advanced Circular Dependency Detection
                        const detectCircularDependencies = (dependencyGraph) => {
                            const circularDependencies = [];
                            const visited = new Set();
                            const recursionStack = new Set();
                            const pathHistory = [];

                            // Build adjacency list from edges
                            const adjacencyList = new Map();
                            dependencyGraph.nodes.forEach(node => {
                                adjacencyList.set(node.id, []);
                            });
                            
                            dependencyGraph.edges.forEach(edge => {
                                if (!adjacencyList.has(edge.from)) adjacencyList.set(edge.from, []);
                                adjacencyList.get(edge.from).push({
                                    to: edge.to,
                                    type: edge.type,
                                    strength: edge.strength
                                });
                            });

                            // DFS to detect cycles
                            const detectCyclesFrom = (nodeId) => {
                                if (recursionStack.has(nodeId)) {
                                    // Found a cycle
                                    const cycleStart = pathHistory.indexOf(nodeId);
                                    const cycle = pathHistory.slice(cycleStart);
                                    cycle.push(nodeId); // Complete the cycle

                                    const cycleDependency = {
                                        id: `cycle-${circularDependencies.length}`,
                                        signals: cycle,
                                        path: cycle,
                                        severity: calculateCycleSeverity(cycle, dependencyGraph),
                                        type: determineCycleType(cycle, dependencyGraph),
                                        impact: analyzeCycleImpact(cycle, dependencyGraph),
                                        recommendations: generateCycleRecommendations(cycle, dependencyGraph)
                                    };

                                    circularDependencies.push(cycleDependency);
                                    return true;
                                }

                                if (visited.has(nodeId)) {
                                    return false;
                                }

                                visited.add(nodeId);
                                recursionStack.add(nodeId);
                                pathHistory.push(nodeId);

                                const neighbors = adjacencyList.get(nodeId) || [];
                                for (const neighbor of neighbors) {
                                    if (detectCyclesFrom(neighbor.to)) {
                                        return true;
                                    }
                                }

                                recursionStack.delete(nodeId);
                                pathHistory.pop();
                                return false;
                            };

                            // Check for cycles starting from each node
                            dependencyGraph.nodes.forEach(node => {
                                if (!visited.has(node.id)) {
                                    detectCyclesFrom(node.id);
                                }
                            });

                            return circularDependencies;
                        };

                        const calculateCycleSeverity = (cycle, graph) => {
                            // Calculate severity based on cycle length and signal types
                            const cycleLength = cycle.length;
                            const signalTypes = cycle.map(signalId => 
                                graph.nodes.find(n => n.id === signalId)?.type || 'unknown'
                            );

                            let severity = 'low';
                            
                            if (cycleLength <= 2) severity = 'critical';
                            else if (cycleLength <= 4) severity = 'high';
                            else if (cycleLength <= 6) severity = 'medium';
                            
                            // Increase severity if computed signals are involved
                            if (signalTypes.includes('computed')) {
                                const severityLevels = ['low', 'medium', 'high', 'critical'];
                                const currentIndex = severityLevels.indexOf(severity);
                                if (currentIndex < severityLevels.length - 1) {
                                    severity = severityLevels[currentIndex + 1];
                                }
                            }

                            return severity;
                        };

                        const determineCycleType = (cycle, graph) => {
                            const signalTypes = cycle.map(signalId => 
                                graph.nodes.find(n => n.id === signalId)?.type || 'unknown'
                            );

                            if (signalTypes.includes('computed')) return 'computed-cycle';
                            if (signalTypes.includes('effect')) return 'effect-cycle';
                            return 'signal-cycle';
                        };

                        const analyzeCycleImpact = (cycle, graph) => {
                            return {
                                affectedComponents: [...new Set(cycle.map(signalId => 
                                    graph.nodes.find(n => n.id === signalId)?.component
                                ))],
                                cycleLength: cycle.length,
                                potentialPerformanceIssues: cycle.length > 3,
                                memoryLeakRisk: cycle.some(signalId => 
                                    graph.nodes.find(n => n.id === signalId)?.type === 'effect'
                                )
                            };
                        };

                        const generateCycleRecommendations = (cycle, graph) => {
                            const recommendations = [];
                            
                            recommendations.push('Consider breaking the circular dependency by introducing intermediate computed signals');
                            
                            if (cycle.length > 4) {
                                recommendations.push('Refactor to reduce the dependency chain length');
                            }
                            
                            const hasEffects = cycle.some(signalId => 
                                graph.nodes.find(n => n.id === signalId)?.type === 'effect'
                            );
                            
                            if (hasEffects) {
                                recommendations.push('Review effect dependencies to prevent infinite loops');
                            }

                            return recommendations;
                        };

                        // Step 5: Comprehensive Analysis and Metrics
                        const performComprehensiveAnalysis = (signals, dependencyGraph, circularDeps) => {
                            const analysis = {
                                totalSignals: signals.length,
                                dependentSignals: 0,
                                independentSignals: 0,
                                maxDependencyDepth: 0,
                                complexityScore: 0,
                                isolationScore: 0,
                                recommendations: [],
                                performanceRisks: [],
                                architectureInsights: []
                            };

                            // Calculate basic metrics
                            const writableSignals = signals.filter(s => s.type === 'writable');
                            const computedSignals = signals.filter(s => s.type === 'computed');
                            const effectSignals = signals.filter(s => s.type === 'effect');

                            analysis.dependentSignals = computedSignals.length + effectSignals.length;
                            analysis.independentSignals = writableSignals.length;

                            // Calculate complexity score
                            analysis.complexityScore = calculateOverallComplexity(signals, dependencyGraph);

                            // Calculate isolation score
                            analysis.isolationScore = calculateIsolationScore(signals, dependencyGraph);

                            // Calculate max dependency depth
                            analysis.maxDependencyDepth = calculateMaxDependencyDepth(dependencyGraph);

                            // Generate recommendations
                            analysis.recommendations = generateArchitectureRecommendations(signals, dependencyGraph, circularDeps);

                            // Identify performance risks
                            analysis.performanceRisks = identifyPerformanceRisks(signals, dependencyGraph);

                            // Generate architecture insights
                            analysis.architectureInsights = generateArchitectureInsights(signals, dependencyGraph);

                            return analysis;
                        };

                        const calculateOverallComplexity = (signals, graph) => {
                            let complexity = 0;
                            
                            // Base complexity from signal count
                            complexity += signals.length * 0.1;
                            
                            // Add complexity from dependency edges
                            complexity += graph.edges.length * 0.2;
                            
                            // Add complexity from circular dependencies
                            const circularCount = graph.edges.filter(e => e.type === 'circular').length;
                            complexity += circularCount * 2;

                            return Math.min(complexity, 10); // Cap at 10
                        };

                        const calculateIsolationScore = (signals, graph) => {
                            // Calculate how well isolated components are
                            const componentGroups = {};
                            signals.forEach(signal => {
                                if (!componentGroups[signal.componentName]) {
                                    componentGroups[signal.componentName] = [];
                                }
                                componentGroups[signal.componentName].push(signal);
                            });

                            const crossComponentEdges = graph.edges.filter(e => e.type === 'cross-component');
                            const totalEdges = graph.edges.length;
                            
                            if (totalEdges === 0) return 10;
                            
                            const isolationRatio = 1 - (crossComponentEdges.length / totalEdges);
                            return Math.round(isolationRatio * 10);
                        };

                        const calculateMaxDependencyDepth = (graph) => {
                            // Use BFS to find the longest dependency chain
                            let maxDepth = 0;
                            
                            // Build adjacency list
                            const adjacencyList = new Map();
                            graph.nodes.forEach(node => {
                                adjacencyList.set(node.id, []);
                            });
                            
                            graph.edges.forEach(edge => {
                                if (!adjacencyList.has(edge.from)) adjacencyList.set(edge.from, []);
                                adjacencyList.get(edge.from).push(edge.to);
                            });

                            // DFS to find maximum depth
                            const findDepth = (nodeId, visited = new Set()) => {
                                if (visited.has(nodeId)) return 0; // Prevent infinite loops
                                
                                visited.add(nodeId);
                                const neighbors = adjacencyList.get(nodeId) || [];
                                
                                if (neighbors.length === 0) return 1;
                                
                                let maxChildDepth = 0;
                                neighbors.forEach(neighbor => {
                                    const depth = findDepth(neighbor, new Set(visited));
                                    maxChildDepth = Math.max(maxChildDepth, depth);
                                });
                                
                                return 1 + maxChildDepth;
                            };

                            graph.nodes.forEach(node => {
                                const depth = findDepth(node.id);
                                maxDepth = Math.max(maxDepth, depth);
                            });

                            return maxDepth;
                        };

                        const generateArchitectureRecommendations = (signals, graph, circularDeps) => {
                            const recommendations = [];

                            // Signal count recommendations
                            if (signals.length > 50) {
                                recommendations.push({
                                    type: 'architecture',
                                    severity: 'medium',
                                    title: 'High Signal Count',
                                    message: `${signals.length} signals detected - consider component decomposition`,
                                    details: 'Large numbers of signals in a single application can indicate over-granular state management'
                                });
                            }

                            // Circular dependency recommendations
                            if (circularDeps.length > 0) {
                                recommendations.push({
                                    type: 'dependency',
                                    severity: 'high',
                                    title: 'Circular Dependencies Detected',
                                    message: `${circularDeps.length} circular dependencies found`,
                                    details: 'Circular dependencies can cause infinite loops and performance issues'
                                });
                            }

                            // Computed signal recommendations
                            const computedSignals = signals.filter(s => s.type === 'computed');
                            const writableSignals = signals.filter(s => s.type === 'writable');
                            
                            if (computedSignals.length === 0 && writableSignals.length > 5) {
                                recommendations.push({
                                    type: 'optimization',
                                    severity: 'low',
                                    title: 'Consider Computed Signals',
                                    message: 'No computed signals found despite having multiple writable signals',
                                    details: 'Computed signals can help derive state and improve performance'
                                });
                            }

                            return recommendations;
                        };

                        const identifyPerformanceRisks = (signals, graph) => {
                            const risks = [];

                            // High complexity signals
                            const complexSignals = signals.filter(s => s.currentValue && s.currentValue.length > 1000);
                            if (complexSignals.length > 0) {
                                risks.push({
                                    type: 'memory',
                                    severity: 'medium',
                                    title: 'Large Signal Values',
                                    affectedSignals: complexSignals.map(s => s.id),
                                    message: `${complexSignals.length} signals with large values detected`
                                });
                            }

                            // Deep dependency chains
                            const maxDepth = calculateMaxDependencyDepth(graph);
                            if (maxDepth > 5) {
                                risks.push({
                                    type: 'performance',
                                    severity: 'medium',
                                    title: 'Deep Dependency Chains',
                                    message: `Maximum dependency depth of ${maxDepth} detected`,
                                    details: 'Deep chains can cause cascading updates'
                                });
                            }

                            return risks;
                        };

                        const generateArchitectureInsights = (signals, graph) => {
                            const insights = [];

                            // Component distribution analysis
                            const componentMap = {};
                            signals.forEach(signal => {
                                if (!componentMap[signal.componentName]) {
                                    componentMap[signal.componentName] = 0;
                                }
                                componentMap[signal.componentName]++;
                            });

                            const componentCount = Object.keys(componentMap).length;
                            const averageSignalsPerComponent = signals.length / componentCount;

                            insights.push({
                                type: 'distribution',
                                title: 'Signal Distribution',
                                message: `${signals.length} signals across ${componentCount} components`,
                                details: `Average ${averageSignalsPerComponent.toFixed(1)} signals per component`
                            });

                            // Signal type distribution
                            const typeDistribution = signals.reduce((acc, signal) => {
                                acc[signal.type] = (acc[signal.type] || 0) + 1;
                                return acc;
                            }, {});

                            insights.push({
                                type: 'types',
                                title: 'Signal Type Distribution',
                                message: 'Signal types in application',
                                details: Object.entries(typeDistribution)
                                    .map(([type, count]) => `${type}: ${count}`)
                                    .join(', ')
                            });

                            return insights;
                        };

                        // Step 6: Quality Metrics Calculation
                        const calculateQualityMetrics = (signals, components, graph, analysis) => {
                            const metrics = {
                                signalToComponentRatio: 0,
                                computedToWritableRatio: 0,
                                dependencyComplexity: 0,
                                circularDependencyCount: 0,
                                unusedSignalCount: 0
                            };

                            if (components.length > 0) {
                                metrics.signalToComponentRatio = Math.round((signals.length / components.length) * 100) / 100;
                            }

                            const writableCount = signals.filter(s => s.type === 'writable').length;
                            const computedCount = signals.filter(s => s.type === 'computed').length;
                            
                            if (writableCount > 0) {
                                metrics.computedToWritableRatio = Math.round((computedCount / writableCount) * 100) / 100;
                            }

                            metrics.dependencyComplexity = Math.round(analysis.complexityScore * 100) / 100;
                            metrics.circularDependencyCount = results.circularDependencies.length;
                            
                            // Estimate unused signals (simplified heuristic)
                            metrics.unusedSignalCount = signals.filter(s => 
                                s.dependencies.length === 0 && s.dependents.length === 0 && s.type === 'writable'
                            ).length;

                            return metrics;
                        };

                        // Main execution flow
                        const executeAnalysis = () => {
                            try {
                                // Step 1: Discover signals and components
                                const discovery = discoverSignalsAdvanced();
                                const { signals, components } = discovery;

                                if (signals.length === 0) {
                                    results.error = 'No signals detected in the application';
                                    return results;
                                }

                                // Categorize signals
                                results.signals.discovered = signals;
                                results.signals.byType = {
                                    writable: signals.filter(s => s.type === 'writable'),
                                    computed: signals.filter(s => s.type === 'computed'),
                                    effect: signals.filter(s => s.type === 'effect')
                                };

                                // Group by component
                                results.signals.byComponent = signals.reduce((acc, signal) => {
                                    if (!acc[signal.componentName]) {
                                        acc[signal.componentName] = [];
                                    }
                                    acc[signal.componentName].push(signal);
                                    return acc;
                                }, {});

                                // Step 2: Build dependency graph
                                const graph = buildDependencyGraph(signals, components);
                                results.dependencyGraph = graph;

                                // Step 3: Detect circular dependencies
                                results.circularDependencies = detectCircularDependencies(graph);

                                // Step 4: Perform comprehensive analysis
                                results.analysis = performComprehensiveAnalysis(signals, graph, results.circularDependencies);

                                // Step 5: Calculate quality metrics
                                results.qualityMetrics = calculateQualityMetrics(signals, components, graph, results.analysis);

                                // Add timing information
                                results.analysisEndTime = Date.now();
                                results.analysisDuration = (results.analysisEndTime - results.analysisStartTime) / 1000;

                                return results;

                            } catch (error) {
                                results.error = `Analysis failed: ${error.message}`;
                                results.errorDetails = error.stack;
                                return results;
                            }
                        };

                        return executeAnalysis();
                    };

                    return analyzeSignalDependencies();
                })();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to analyze signal dependencies: {ex.Message}";
        }
    }
}
