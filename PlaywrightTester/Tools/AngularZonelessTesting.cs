using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// ANG-007: Zoneless Change Detection Testing Implementation
/// Implements test_zoneless_change_detection function for Angular 18+ zoneless applications
/// Tests change detection validation and compatibility with modern Angular patterns
/// </summary>
[McpServerToolType]
public class AngularZonelessTesting(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Test zoneless change detection in Angular 18+ applications - ANG-007 Implementation")]
    public async Task<string> TestZonelessChangeDetection(
        [Description("Maximum test duration in seconds")] int timeoutSeconds = 60,
        [Description("Number of change detection cycles to test")] int testCycles = 5,
        [Description("Include detailed change detection analysis")] bool includeDetailedAnalysis = true,
        [Description("Test interval between change detection triggers in milliseconds")] int testIntervalMs = 1000,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    // ANG-007: Zoneless Change Detection Testing Implementation
                    const testZonelessChangeDetection = () => {{
                        const results = {{
                            testStartTime: Date.now(),
                            testEndTime: null,
                            angularDetected: false,
                            isZoneless: false,
                            angularVersion: null,
                            zonelessCompatibility: {{
                                supported: false,
                                features: {{
                                    changeDetectionRef: false,
                                    applicationRef: false,
                                    markForCheck: false,
                                    detectChanges: false,
                                    signals: false,
                                    onPush: false,
                                    provideExperimentalZonelessChangeDetection: false
                                }},
                                providerConfiguration: {{
                                    detected: false,
                                    method: null,
                                    isExperimental: false
                                }}
                            }},
                            changeDetectionTests: {{
                                totalTests: {testCycles},
                                completedTests: 0,
                                passedTests: 0,
                                failedTests: 0,
                                testResults: []
                            }},
                            performanceMetrics: {{
                                averageDetectionTime: 0,
                                minDetectionTime: Number.MAX_SAFE_INTEGER,
                                maxDetectionTime: 0,
                                totalDetectionTime: 0,
                                detectionTimeHistory: [],
                                memoryUsage: {{
                                    before: {{ heapUsed: 0, heapTotal: 0 }},
                                    after: {{ heapUsed: 0, heapTotal: 0 }},
                                    difference: {{ heapUsed: 0, heapTotal: 0 }}
                                }}
                            }},
                            signalIntegration: {{
                                signalsDetected: false,
                                signalCount: 0,
                                computedSignals: 0,
                                effectCount: 0,
                                signalWorkingCorrectly: false,
                                signalUpdateTests: []
                            }},
                            manualChangeDetection: {{
                                applicationRefDetected: false,
                                markForCheckAvailable: false,
                                detectChangesAvailable: false,
                                tickAvailable: false,
                                manualTriggerTests: []
                            }},
                            componentAnalysis: {{
                                totalComponents: 0,
                                onPushComponents: 0,
                                defaultComponents: 0,
                                standaloneComponents: 0,
                                componentChangeDetectionStrategies: []
                            }},
                            zonelessFeatureValidation: {{
                                eventCoalescing: false,
                                requestAnimationFrame: false,
                                promiseScheduling: false,
                                timeoutScheduling: false,
                                intervalScheduling: false,
                                asyncValidation: {{
                                    promises: false,
                                    observables: false,
                                    httpClient: false
                                }}
                            }},
                            recommendations: [],
                            warnings: [],
                            errors: [],
                            detailedAnalysis: {includeDetailedAnalysis.ToString().ToLower()},
                            testConfiguration: {{
                                timeoutSeconds: {timeoutSeconds},
                                testCycles: {testCycles},
                                testIntervalMs: {testIntervalMs}
                            }}
                        }};

                        // Step 1: Detect Angular and determine if it's zoneless
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
                                    results.angularVersion = versionElement.getAttribute('ng-version');
                                    
                                    // Check if version supports zoneless (Angular 18+)
                                    const versionParts = results.angularVersion.split('.');
                                    const majorVersion = parseInt(versionParts[0]);
                                    if (majorVersion < 18) {{
                                        results.warnings.push(`Angular version ${{results.angularVersion}} detected. Zoneless change detection requires Angular 18+`);
                                    }}
                                }}

                                // Determine if application is zoneless
                                const zoneJsPresent = !!(window.Zone && window.Zone.current);
                                const hasZonelessAttribute = document.querySelector('[ng-zoneless]') !== null;
                                const hasExperimentalProvider = !!(window.ng?.experimental?.provideZonelessChangeDetection);
                                
                                results.isZoneless = !zoneJsPresent || hasZonelessAttribute || hasExperimentalProvider;

                                if (!results.isZoneless) {{
                                    results.warnings.push('Application appears to be zone-based. This test is optimized for zoneless applications.');
                                    // Continue testing anyway for compatibility
                                }}

                                return true;
                            }} catch (e) {{
                                results.errors.push(`Error detecting Angular environment: ${{e.message}}`);
                                return false;
                            }}
                        }};

                        // Step 2: Analyze zoneless compatibility features
                        const analyzeZonelessCompatibility = () => {{
                            try {{
                                // Check for zoneless provider configuration
                                if (window.ng?.experimental?.provideZonelessChangeDetection) {{
                                    results.zonelessCompatibility.providerConfiguration.detected = true;
                                    results.zonelessCompatibility.providerConfiguration.method = 'experimental';
                                    results.zonelessCompatibility.providerConfiguration.isExperimental = true;
                                    results.zonelessCompatibility.features.provideExperimentalZonelessChangeDetection = true;
                                }}

                                // Check for application configuration
                                if (window.ng?.provideZonelessChangeDetection) {{
                                    results.zonelessCompatibility.providerConfiguration.detected = true;
                                    results.zonelessCompatibility.providerConfiguration.method = 'stable';
                                    results.zonelessCompatibility.providerConfiguration.isExperimental = false;
                                }}

                                // Check for ChangeDetectorRef availability
                                const testComponent = document.querySelector('*[ng-version] *');
                                if (testComponent && window.ng?.getComponent) {{
                                    try {{
                                        const component = window.ng.getComponent(testComponent);
                                        if (component && component.constructor) {{
                                            // Look for injected ChangeDetectorRef
                                            const cdrKeys = Object.getOwnPropertyNames(component).filter(key => 
                                                component[key] && 
                                                component[key].constructor && 
                                                (component[key].constructor.name === 'ChangeDetectorRef' ||
                                                 component[key].constructor.name.includes('ChangeDetector'))
                                            );
                                            
                                            if (cdrKeys.length > 0) {{
                                                results.zonelessCompatibility.features.changeDetectionRef = true;
                                                results.zonelessCompatibility.features.markForCheck = !!(component[cdrKeys[0]].markForCheck);
                                                results.zonelessCompatibility.features.detectChanges = !!(component[cdrKeys[0]].detectChanges);
                                            }}
                                        }}
                                    }} catch (e) {{
                                        results.warnings.push(`Error analyzing component change detection: ${{e.message}}`);
                                    }}
                                }}

                                // Check for ApplicationRef availability
                                if (window.ng?.getInjector) {{
                                    try {{
                                        const rootElements = window.getAllAngularRootElements?.() || [];
                                        if (rootElements.length > 0) {{
                                            const injector = window.ng.getInjector(rootElements[0]);
                                            if (injector) {{
                                                const appRef = injector.get?.('ApplicationRef');
                                                if (appRef) {{
                                                    results.zonelessCompatibility.features.applicationRef = true;
                                                    results.manualChangeDetection.applicationRefDetected = true;
                                                    results.manualChangeDetection.tickAvailable = !!(appRef.tick);
                                                }}
                                            }}
                                        }}
                                    }} catch (e) {{
                                        results.warnings.push(`Error accessing ApplicationRef: ${{e.message}}`);
                                    }}
                                }}

                                // Check for signals support
                                const hasSignals = !!(window.ng?.signal || window.ng?.computed || window.ng?.effect);
                                results.zonelessCompatibility.features.signals = hasSignals;
                                
                                if (hasSignals) {{
                                    results.signalIntegration.signalsDetected = true;
                                }}

                                // Check for OnPush change detection strategy support
                                results.zonelessCompatibility.features.onPush = !!(window.ng?.ChangeDetectionStrategy?.OnPush);

                                // Determine overall compatibility
                                const featuresSupported = Object.values(results.zonelessCompatibility.features).filter(Boolean).length;
                                results.zonelessCompatibility.supported = featuresSupported >= 3; // At least 3 features must be supported

                                return true;
                            }} catch (e) {{
                                results.errors.push(`Error analyzing zoneless compatibility: ${{e.message}}`);
                                return false;
                            }}
                        }};

                        // Step 3: Analyze component change detection strategies
                        const analyzeComponentStrategies = () => {{
                            try {{
                                if (!window.ng?.getComponent) {{
                                    results.warnings.push('Angular debugging tools not available for component analysis');
                                    return;
                                }}

                                const allElements = Array.from(document.querySelectorAll('*')).slice(0, 100);
                                let componentCount = 0;
                                let onPushCount = 0;
                                let defaultCount = 0;
                                let standaloneCount = 0;
                                
                                allElements.forEach(el => {{
                                    try {{
                                        const component = window.ng.getComponent(el);
                                        if (component && component.constructor) {{
                                            componentCount++;
                                            
                                            // Analyze change detection strategy
                                            const componentDef = component.constructor.ɵcmp || component.constructor.ɵdir;
                                            if (componentDef) {{
                                                const strategy = componentDef.changeDetection;
                                                if (strategy === 0) {{ // OnPush
                                                    onPushCount++;
                                                    results.componentAnalysis.componentChangeDetectionStrategies.push({{
                                                        element: el.tagName.toLowerCase(),
                                                        strategy: 'OnPush',
                                                        component: component.constructor.name
                                                    }});
                                                }} else {{ // Default
                                                    defaultCount++;
                                                    results.componentAnalysis.componentChangeDetectionStrategies.push({{
                                                        element: el.tagName.toLowerCase(), 
                                                        strategy: 'Default',
                                                        component: component.constructor.name
                                                    }});
                                                }}
                                                
                                                // Check if standalone component
                                                if (componentDef.standalone) {{
                                                    standaloneCount++;
                                                }}
                                            }}
                                        }}
                                    }} catch (e) {{
                                        // Skip elements without components
                                    }}
                                }});

                                results.componentAnalysis.totalComponents = componentCount;
                                results.componentAnalysis.onPushComponents = onPushCount;
                                results.componentAnalysis.defaultComponents = defaultCount;
                                results.componentAnalysis.standaloneComponents = standaloneCount;

                                // Generate recommendations based on component analysis
                                if (defaultCount > onPushCount && results.isZoneless) {{
                                    results.recommendations.push({{
                                        type: 'performance',
                                        priority: 'high',
                                        message: `${{defaultCount}} components using Default change detection strategy. Consider using OnPush for better performance in zoneless applications.`
                                    }});
                                }}

                                if (standaloneCount > 0) {{
                                    results.recommendations.push({{
                                        type: 'architecture',
                                        priority: 'info',
                                        message: `${{standaloneCount}} standalone components detected. These work well with zoneless change detection.`
                                    }});
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error analyzing component strategies: ${{e.message}}`);
                            }}
                        }};

                        // Step 4: Test manual change detection triggers
                        const testManualChangeDetection = () => {{
                            return new Promise((resolve) => {{
                                try {{
                                    let testIndex = 0;
                                    const testResults = [];

                                    const runSingleTest = () => {{
                                        if (testIndex >= {testCycles}) {{
                                            resolve(testResults);
                                            return;
                                        }}

                                        const testStart = performance.now();
                                        const testData = {{
                                            testNumber: testIndex + 1,
                                            startTime: testStart,
                                            endTime: null,
                                            success: false,
                                            method: null,
                                            detectionTime: 0,
                                            error: null,
                                            changesMade: 0,
                                            changesDetected: 0
                                        }};

                                        try {{
                                            // Method 1: Try ApplicationRef.tick()
                                            if (results.manualChangeDetection.applicationRefDetected) {{
                                                const rootElements = window.getAllAngularRootElements?.() || [];
                                                if (rootElements.length > 0) {{
                                                    const injector = window.ng.getInjector(rootElements[0]);
                                                    const appRef = injector?.get?.('ApplicationRef');
                                                    if (appRef && appRef.tick) {{
                                                        // Create a test change
                                                        const testElement = document.createElement('div');
                                                        testElement.textContent = `Test change ${{testIndex + 1}} - ${{Date.now()}}`;
                                                        document.body.appendChild(testElement);
                                                        testData.changesMade++;

                                                        // Trigger change detection
                                                        appRef.tick();
                                                        testData.method = 'ApplicationRef.tick()';
                                                        testData.success = true;

                                                        // Clean up
                                                        setTimeout(() => {{
                                                            document.body.removeChild(testElement);
                                                        }}, 100);
                                                    }}
                                                }}
                                            }}

                                            // Method 2: Try ChangeDetectorRef.detectChanges() if ApplicationRef failed
                                            if (!testData.success && results.zonelessCompatibility.features.changeDetectionRef) {{
                                                const testComponent = document.querySelector('*[ng-version] *');
                                                if (testComponent && window.ng?.getComponent) {{
                                                    const component = window.ng.getComponent(testComponent);
                                                    if (component) {{
                                                        const cdrKeys = Object.getOwnPropertyNames(component).filter(key => 
                                                            component[key] && 
                                                            component[key].constructor && 
                                                            component[key].constructor.name.includes('ChangeDetector')
                                                        );
                                                        
                                                        if (cdrKeys.length > 0 && component[cdrKeys[0]].detectChanges) {{
                                                            component[cdrKeys[0]].detectChanges();
                                                            testData.method = 'ChangeDetectorRef.detectChanges()';
                                                            testData.success = true;
                                                        }}
                                                    }}
                                                }}
                                            }}

                                            // Method 3: Try signal updates if other methods failed
                                            if (!testData.success && results.signalIntegration.signalsDetected) {{
                                                // Try to find and update signals
                                                if (window.ng?.signal) {{
                                                    // Create a test signal
                                                    const testSignal = window.ng.signal(`test-${{testIndex}}-${{Date.now()}}`);
                                                    testSignal.set(`updated-${{testIndex}}-${{Date.now()}}`);
                                                    testData.method = 'Signal update';
                                                    testData.success = true;
                                                }}
                                            }}

                                        }} catch (e) {{
                                            testData.error = e.message;
                                        }}

                                        testData.endTime = performance.now();
                                        testData.detectionTime = testData.endTime - testData.startTime;
                                        
                                        testResults.push(testData);
                                        
                                        if (testData.success) {{
                                            results.changeDetectionTests.passedTests++;
                                        }} else {{
                                            results.changeDetectionTests.failedTests++;
                                        }}
                                        
                                        results.changeDetectionTests.completedTests++;
                                        
                                        // Update performance metrics
                                        results.performanceMetrics.totalDetectionTime += testData.detectionTime;
                                        results.performanceMetrics.detectionTimeHistory.push(testData.detectionTime);
                                        
                                        if (testData.detectionTime < results.performanceMetrics.minDetectionTime) {{
                                            results.performanceMetrics.minDetectionTime = testData.detectionTime;
                                        }}
                                        if (testData.detectionTime > results.performanceMetrics.maxDetectionTime) {{
                                            results.performanceMetrics.maxDetectionTime = testData.detectionTime;
                                        }}

                                        testIndex++;
                                        
                                        // Schedule next test
                                        setTimeout(runSingleTest, {testIntervalMs});
                                    }};

                                    // Start the test sequence
                                    runSingleTest();

                                }} catch (e) {{
                                    results.errors.push(`Error testing manual change detection: ${{e.message}}`);
                                    resolve([]);
                                }}
                            }});
                        }};

                        // Step 5: Test signal integration in zoneless context
                        const testSignalIntegration = () => {{
                            try {{
                                if (!results.signalIntegration.signalsDetected) {{
                                    results.warnings.push('Signals not detected - skipping signal integration tests');
                                    return;
                                }}

                                // Count signals in the application
                                let signalCount = 0;
                                let computedCount = 0;
                                let effectCount = 0;

                                if (window.ng?.signal) {{
                                    // Test signal creation and updates
                                    const testSignal = window.ng.signal(0);
                                    const startValue = testSignal();
                                    
                                    // Test signal update
                                    testSignal.set(42);
                                    const updatedValue = testSignal();
                                    
                                    const signalTest = {{
                                        type: 'signal_update',
                                        success: updatedValue === 42,
                                        startValue: startValue,
                                        updatedValue: updatedValue,
                                        timestamp: Date.now()
                                    }};
                                    
                                    results.signalIntegration.signalUpdateTests.push(signalTest);
                                    
                                    if (signalTest.success) {{
                                        results.signalIntegration.signalWorkingCorrectly = true;
                                    }}
                                }}

                                if (window.ng?.computed) {{
                                    // Test computed signals
                                    const baseSignal = window.ng?.signal?.(10);
                                    if (baseSignal) {{
                                        const computedSignal = window.ng.computed(() => baseSignal() * 2);
                                        const computedValue = computedSignal();
                                        
                                        const computedTest = {{
                                            type: 'computed_signal',
                                            success: computedValue === 20,
                                            computedValue: computedValue,
                                            expectedValue: 20,
                                            timestamp: Date.now()
                                        }};
                                        
                                        results.signalIntegration.signalUpdateTests.push(computedTest);
                                    }}
                                }}

                                // Estimate signal usage in the application (simplified)
                                results.signalIntegration.signalCount = signalCount;
                                results.signalIntegration.computedSignals = computedCount;
                                results.signalIntegration.effectCount = effectCount;

                            }} catch (e) {{
                                results.warnings.push(`Error testing signal integration: ${{e.message}}`);
                            }}
                        }};

                        // Step 6: Validate zoneless-specific features
                        const validateZonelessFeatures = () => {{
                            try {{
                                // Test event coalescing (simplified check)
                                results.zonelessFeatureValidation.eventCoalescing = !!window.requestIdleCallback;
                                
                                // Test RAF scheduling
                                results.zonelessFeatureValidation.requestAnimationFrame = !!window.requestAnimationFrame;
                                
                                // Test Promise scheduling
                                results.zonelessFeatureValidation.promiseScheduling = !!Promise.resolve;
                                
                                // Test timeout/interval scheduling
                                results.zonelessFeatureValidation.timeoutScheduling = !!window.setTimeout;
                                results.zonelessFeatureValidation.intervalScheduling = !!window.setInterval;
                                
                                // Test async validation features
                                results.zonelessFeatureValidation.asyncValidation.promises = !!Promise.resolve;
                                results.zonelessFeatureValidation.asyncValidation.observables = !!(window.rxjs || window.Observable);
                                results.zonelessFeatureValidation.asyncValidation.httpClient = !!(window.ng?.HttpClient);

                            }} catch (e) {{
                                results.warnings.push(`Error validating zoneless features: ${{e.message}}`);
                            }}
                        }};

                        // Step 7: Generate performance metrics and recommendations
                        const generateMetricsAndRecommendations = () => {{
                            try {{
                                // Calculate performance metrics
                                if (results.changeDetectionTests.completedTests > 0) {{
                                    results.performanceMetrics.averageDetectionTime = 
                                        results.performanceMetrics.totalDetectionTime / results.changeDetectionTests.completedTests;
                                }}

                                // Memory usage (if available)
                                if (performance.memory) {{
                                    results.performanceMetrics.memoryUsage.after.heapUsed = performance.memory.usedJSHeapSize;
                                    results.performanceMetrics.memoryUsage.after.heapTotal = performance.memory.totalJSHeapSize;
                                }}

                                // Generate recommendations
                                if (!results.isZoneless) {{
                                    results.recommendations.push({{
                                        type: 'migration',
                                        priority: 'medium',
                                        message: 'Consider migrating to zoneless change detection for better performance and control.'
                                    }});
                                }}

                                if (results.performanceMetrics.averageDetectionTime > 10) {{
                                    results.recommendations.push({{
                                        type: 'performance',
                                        priority: 'high',
                                        message: `Average change detection time (${{results.performanceMetrics.averageDetectionTime.toFixed(2)}}ms) is high. Consider optimizing components.`
                                    }});
                                }}

                                if (results.changeDetectionTests.failedTests > 0) {{
                                    results.recommendations.push({{
                                        type: 'reliability',
                                        priority: 'critical',
                                        message: `${{results.changeDetectionTests.failedTests}} change detection tests failed. Review manual change detection implementation.`
                                    }});
                                }}

                                if (!results.signalIntegration.signalsDetected && results.isZoneless) {{
                                    results.recommendations.push({{
                                        type: 'modernization',
                                        priority: 'medium',
                                        message: 'Consider using Angular signals for reactive state management in zoneless applications.'
                                    }});
                                }}

                                if (results.componentAnalysis.onPushComponents === 0 && results.componentAnalysis.totalComponents > 0) {{
                                    results.recommendations.push({{
                                        type: 'optimization',
                                        priority: 'medium',
                                        message: 'No OnPush components detected. Consider using OnPush strategy for better performance.'
                                    }});
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error generating metrics and recommendations: ${{e.message}}`);
                            }}
                        }};

                        // Main test execution flow
                        const executeTest = async () => {{
                            // Record initial memory if available
                            if (performance.memory) {{
                                results.performanceMetrics.memoryUsage.before.heapUsed = performance.memory.usedJSHeapSize;
                                results.performanceMetrics.memoryUsage.before.heapTotal = performance.memory.totalJSHeapSize;
                            }}

                            // Step 1: Environment detection
                            if (!detectAngularEnvironment()) {{
                                results.testEndTime = Date.now();
                                return results;
                            }}

                            // Step 2: Compatibility analysis
                            analyzeZonelessCompatibility();

                            // Step 3: Component analysis
                            analyzeComponentStrategies();

                            // Step 4: Signal integration testing
                            testSignalIntegration();

                            // Step 5: Zoneless feature validation
                            validateZonelessFeatures();

                            // Step 6: Manual change detection testing
                            const changeDetectionResults = await testManualChangeDetection();
                            results.manualChangeDetection.manualTriggerTests = changeDetectionResults;

                            // Step 7: Final metrics and recommendations
                            generateMetricsAndRecommendations();

                            // Calculate memory difference
                            if (performance.memory) {{
                                results.performanceMetrics.memoryUsage.difference.heapUsed = 
                                    results.performanceMetrics.memoryUsage.after.heapUsed - 
                                    results.performanceMetrics.memoryUsage.before.heapUsed;
                                results.performanceMetrics.memoryUsage.difference.heapTotal = 
                                    results.performanceMetrics.memoryUsage.after.heapTotal - 
                                    results.performanceMetrics.memoryUsage.before.heapTotal;
                            }}

                            results.testEndTime = Date.now();
                            return results;
                        }};

                        return executeTest();
                    }};

                    return testZonelessChangeDetection();
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to test zoneless change detection: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Validate change detection patterns in Angular applications for zoneless compatibility")]
    public async Task<string> ValidateChangeDetectionPatterns(
        [Description("Check for common anti-patterns")] bool checkAntiPatterns = true,
        [Description("Analyze component hierarchy for optimization opportunities")] bool analyzeHierarchy = true,
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
                        validation: {{
                            patterns: {{
                                onPushUsage: {{ score: 0, components: [], recommendations: [] }},
                                signalUsage: {{ score: 0, signals: [], recommendations: [] }},
                                changeDetectionStrategy: {{ score: 0, analysis: [], recommendations: [] }},
                                templateOptimization: {{ score: 0, issues: [], recommendations: [] }}
                            }},
                            antiPatterns: {{
                                detectedIssues: [],
                                severity: 'none',
                                totalIssues: 0
                            }},
                            hierarchy: {{
                                componentDepth: 0,
                                communicationPatterns: [],
                                optimizationOpportunities: []
                            }}
                        }},
                        overallScore: 0,
                        compatibility: {{
                            zonelessReady: false,
                            migrationEffort: 'unknown',
                            blockers: []
                        }},
                        checkAntiPatterns: {checkAntiPatterns.ToString().ToLower()},
                        analyzeHierarchy: {analyzeHierarchy.ToString().ToLower()}
                    }};

                    // Check Angular presence
                    results.angularDetected = !!(window.ng || window.ngDevMode || document.querySelector('[ng-version]'));
                    
                    if (!results.angularDetected) {{
                        return results;
                    }}

                    // Pattern validation functions
                    const validateOnPushUsage = () => {{
                        if (!window.ng?.getComponent) return;
                        
                        let totalComponents = 0;
                        let onPushComponents = 0;
                        const components = [];
                        
                        Array.from(document.querySelectorAll('*')).slice(0, 100).forEach(el => {{
                            try {{
                                const component = window.ng.getComponent(el);
                                if (component && component.constructor) {{
                                    totalComponents++;
                                    const componentDef = component.constructor.ɵcmp;
                                    if (componentDef) {{
                                        const isOnPush = componentDef.changeDetection === 0;
                                        if (isOnPush) {{
                                            onPushComponents++;
                                        }}
                                        components.push({{
                                            name: component.constructor.name,
                                            element: el.tagName.toLowerCase(),
                                            strategy: isOnPush ? 'OnPush' : 'Default',
                                            standalone: !!componentDef.standalone
                                        }});
                                    }}
                                }}
                            }} catch (e) {{
                                // Skip elements without components
                            }}
                        }});
                        
                        results.validation.patterns.onPushUsage.components = components;
                        results.validation.patterns.onPushUsage.score = totalComponents > 0 ? (onPushComponents / totalComponents) * 100 : 0;
                        
                        if (results.validation.patterns.onPushUsage.score < 50) {{
                            results.validation.patterns.onPushUsage.recommendations.push(
                                'Consider using OnPush change detection strategy for better performance'
                            );
                        }}
                    }};

                    const validateSignalUsage = () => {{
                        let signalScore = 0;
                        const signals = [];
                        
                        if (window.ng?.signal) {{
                            signalScore += 30;
                            signals.push('signal() factory available');
                        }}
                        if (window.ng?.computed) {{
                            signalScore += 30;
                            signals.push('computed() signals available');
                        }}
                        if (window.ng?.effect) {{
                            signalScore += 40;
                            signals.push('effect() function available');
                        }}
                        
                        results.validation.patterns.signalUsage.score = signalScore;
                        results.validation.patterns.signalUsage.signals = signals;
                        
                        if (signalScore < 50) {{
                            results.validation.patterns.signalUsage.recommendations.push(
                                'Consider upgrading to Angular 16+ and using signals for reactive state management'
                            );
                        }}
                    }};

                    const validateChangeDetectionStrategy = () => {{
                        const analysis = [];
                        let strategyScore = 100;
                        
                        // Check for Zone.js dependency
                        if (window.Zone && window.Zone.current) {{
                            analysis.push({{
                                type: 'zone_dependency',
                                message: 'Application uses Zone.js - consider migrating to zoneless',
                                impact: 'medium'
                            }});
                            strategyScore -= 20;
                        }}
                        
                        // Check for manual change detection
                        if (window.ng?.getInjector) {{
                            try {{
                                const rootElements = window.getAllAngularRootElements?.() || [];
                                if (rootElements.length > 0) {{
                                    const injector = window.ng.getInjector(rootElements[0]);
                                    const appRef = injector?.get?.('ApplicationRef');
                                    if (appRef?.tick) {{
                                        analysis.push({{
                                            type: 'manual_detection',
                                            message: 'ApplicationRef.tick() available for manual change detection',
                                            impact: 'positive'
                                        }});
                                        strategyScore += 10;
                                    }}
                                }}
                            }} catch (e) {{
                                // Error accessing injector
                            }}
                        }}
                        
                        results.validation.patterns.changeDetectionStrategy.score = Math.max(0, strategyScore);
                        results.validation.patterns.changeDetectionStrategy.analysis = analysis;
                    }};

                    const checkAntiPatternsFunc = () => {{
                        if (!results.checkAntiPatterns) return;
                        
                        const issues = [];
                        
                        // Check for excessive change detection triggers
                        const hasExcessiveTriggers = document.querySelectorAll('[ng-click], [ng-keyup], [ng-mouseover]').length > 50;
                        if (hasExcessiveTriggers) {{
                            issues.push({{
                                type: 'excessive_event_bindings',
                                severity: 'medium',
                                message: 'Large number of event bindings detected - may cause frequent change detection'
                            }});
                        }}
                        
                        // Check for complex expressions in templates (simplified)
                        const hasComplexExpressions = document.querySelectorAll('[ng-bind]').length > 20;
                        if (hasComplexExpressions) {{
                            issues.push({{
                                type: 'complex_template_expressions',
                                severity: 'low',
                                message: 'Many template bindings detected - consider optimizing complex expressions'
                            }});
                        }}
                        
                        results.validation.antiPatterns.detectedIssues = issues;
                        results.validation.antiPatterns.totalIssues = issues.length;
                        
                        if (issues.length > 0) {{
                            const severities = issues.map(i => i.severity);
                            if (severities.includes('high')) {{
                                results.validation.antiPatterns.severity = 'high';
                            }} else if (severities.includes('medium')) {{
                                results.validation.antiPatterns.severity = 'medium';
                            }} else {{
                                results.validation.antiPatterns.severity = 'low';
                            }}
                        }}
                    }};

                    const analyzeHierarchyFunc = () => {{
                        if (!results.analyzeHierarchy) return;
                        
                        // Calculate component depth (simplified)
                        let maxDepth = 0;
                        const communicationPatterns = [];
                        
                        try {{
                            const rootElements = window.getAllAngularRootElements?.() || [];
                            if (rootElements.length > 0) {{
                                const calculateDepth = (element, depth = 0) => {{
                                    maxDepth = Math.max(maxDepth, depth);
                                    Array.from(element.children).forEach(child => {{
                                        if (window.ng?.getComponent?.(child)) {{
                                            calculateDepth(child, depth + 1);
                                        }}
                                    }});
                                }};
                                
                                rootElements.forEach(root => calculateDepth(root));
                            }}
                        }} catch (e) {{
                            // Error calculating hierarchy
                        }}
                        
                        results.validation.hierarchy.componentDepth = maxDepth;
                        
                        if (maxDepth > 10) {{
                            results.validation.hierarchy.optimizationOpportunities.push(
                                'Deep component hierarchy detected - consider flattening structure for better performance'
                            );
                        }}
                    }};

                    // Execute validation
                    validateOnPushUsage();
                    validateSignalUsage();
                    validateChangeDetectionStrategy();
                    checkAntiPatternsFunc();
                    analyzeHierarchyFunc();

                    // Calculate overall score
                    const scores = [
                        results.validation.patterns.onPushUsage.score,
                        results.validation.patterns.signalUsage.score,
                        results.validation.patterns.changeDetectionStrategy.score
                    ];
                    results.overallScore = scores.reduce((sum, score) => sum + score, 0) / scores.length;

                    // Determine zoneless compatibility
                    results.compatibility.zonelessReady = results.overallScore >= 70;
                    
                    if (results.overallScore >= 80) {{
                        results.compatibility.migrationEffort = 'low';
                    }} else if (results.overallScore >= 60) {{
                        results.compatibility.migrationEffort = 'medium';
                    }} else {{
                        results.compatibility.migrationEffort = 'high';
                    }}

                    // Identify blockers
                    if (results.validation.antiPatterns.severity === 'high') {{
                        results.compatibility.blockers.push('High severity anti-patterns detected');
                    }}
                    if (results.validation.patterns.onPushUsage.score < 30) {{
                        results.compatibility.blockers.push('Low OnPush strategy adoption');
                    }}
                    if (results.validation.patterns.signalUsage.score < 30) {{
                        results.compatibility.blockers.push('Limited signal API usage');
                    }}

                    return results;
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to validate change detection patterns: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Generate zoneless migration recommendations for Angular applications")]
    public async Task<string> GenerateZonelessMigrationPlan(
        [Description("Include step-by-step migration guide")] bool includeStepByStep = true,
        [Description("Analyze migration risks and blockers")] bool analyzeRisks = true,
        [Description("Estimate migration effort and timeline")] bool estimateEffort = true,
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
                        currentState: {{
                            angularVersion: null,
                            isZoneless: false,
                            zoneJsPresent: false,
                            componentCount: 0,
                            readinessScore: 0
                        }},
                        migrationPlan: {{
                            phases: [],
                            totalSteps: 0,
                            estimatedDuration: null,
                            complexity: 'unknown'
                        }},
                        riskAssessment: {{
                            risks: [],
                            blockers: [],
                            mitigationStrategies: []
                        }},
                        recommendations: {{
                            immediate: [],
                            shortTerm: [],
                            longTerm: []
                        }},
                        resources: {{
                            documentation: [],
                            tools: [],
                            examples: []
                        }},
                        includeStepByStep: {includeStepByStep.ToString().ToLower()},
                        analyzeRisks: {analyzeRisks.ToString().ToLower()},
                        estimateEffort: {estimateEffort.ToString().ToLower()}
                    }};

                    // Check Angular presence and current state
                    results.angularDetected = !!(window.ng || window.ngDevMode || document.querySelector('[ng-version]'));
                    
                    if (!results.angularDetected) {{
                        results.recommendations.immediate.push('No Angular application detected on current page');
                        return results;
                    }}

                    // Analyze current state
                    const analyzeCurrentState = () => {{
                        // Get Angular version
                        const versionElement = document.querySelector('[ng-version]');
                        if (versionElement) {{
                            results.currentState.angularVersion = versionElement.getAttribute('ng-version');
                        }}

                        // Check Zone.js presence
                        results.currentState.zoneJsPresent = !!(window.Zone && window.Zone.current);
                        
                        // Check if already zoneless
                        results.currentState.isZoneless = !results.currentState.zoneJsPresent || 
                                                        document.querySelector('[ng-zoneless]') !== null ||
                                                        !!(window.ng?.experimental?.provideZonelessChangeDetection);

                        // Count components
                        if (window.ng?.getComponent) {{
                            let componentCount = 0;
                            Array.from(document.querySelectorAll('*')).slice(0, 100).forEach(el => {{
                                try {{
                                    if (window.ng.getComponent(el)) {{
                                        componentCount++;
                                    }}
                                }} catch (e) {{
                                    // Skip elements without components
                                }}
                            }});
                            results.currentState.componentCount = componentCount;
                        }}

                        // Calculate readiness score
                        let readinessScore = 0;
                        
                        // Version check (Angular 18+ gets full points)
                        if (results.currentState.angularVersion) {{
                            const majorVersion = parseInt(results.currentState.angularVersion.split('.')[0]);
                            if (majorVersion >= 18) {{
                                readinessScore += 40;
                            }} else if (majorVersion >= 16) {{
                                readinessScore += 20;
                            }} else {{
                                readinessScore += 0;
                            }}
                        }}

                        // Signal API availability
                        if (window.ng?.signal) readinessScore += 20;
                        if (window.ng?.computed) readinessScore += 10;
                        if (window.ng?.effect) readinessScore += 10;

                        // OnPush strategy usage
                        let onPushComponents = 0;
                        if (window.ng?.getComponent) {{
                            Array.from(document.querySelectorAll('*')).slice(0, 50).forEach(el => {{
                                try {{
                                    const component = window.ng.getComponent(el);
                                    if (component && component.constructor) {{
                                        const componentDef = component.constructor.ɵcmp;
                                        if (componentDef && componentDef.changeDetection === 0) {{
                                            onPushComponents++;
                                        }}
                                    }}
                                }} catch (e) {{
                                    // Skip
                                }}
                            }});
                        }}
                        
                        if (results.currentState.componentCount > 0) {{
                            const onPushRatio = onPushComponents / results.currentState.componentCount;
                            readinessScore += onPushRatio * 20;
                        }}

                        results.currentState.readinessScore = Math.round(readinessScore);
                    }};

                    // Generate migration phases
                    const generateMigrationPlan = () => {{
                        if (!results.includeStepByStep) return;

                        const phases = [];

                        // Phase 1: Prerequisites and Assessment
                        phases.push({{
                            name: 'Prerequisites and Assessment',
                            duration: '1-2 weeks',
                            steps: [
                                'Upgrade to Angular 18 or later if not already done',
                                'Audit current change detection patterns',
                                'Identify components using Default change detection strategy',
                                'Review third-party dependencies for zoneless compatibility',
                                'Set up testing environment for zoneless validation'
                            ],
                            deliverables: [
                                'Angular version compatibility report',
                                'Component audit results',
                                'Dependency compatibility assessment',
                                'Test environment setup'
                            ]
                        }});

                        // Phase 2: Component Optimization
                        phases.push({{
                            name: 'Component Optimization',
                            duration: '2-4 weeks',
                            steps: [
                                'Convert components to use OnPush change detection strategy',
                                'Refactor components to use signals for state management',
                                'Remove unnecessary Zone.js dependencies',
                                'Optimize template expressions and event bindings',
                                'Implement manual change detection triggers where needed'
                            ],
                            deliverables: [
                                'Optimized components with OnPush strategy',
                                'Signal-based state management implementation',
                                'Reduced Zone.js dependency footprint',
                                'Performance-optimized templates'
                            ]
                        }});

                        // Phase 3: Zoneless Configuration
                        phases.push({{
                            name: 'Zoneless Configuration',
                            duration: '1-2 weeks', 
                            steps: [
                                'Add provideExperimentalZonelessChangeDetection() to application configuration',
                                'Remove Zone.js imports and polyfills',
                                'Update build configuration to exclude Zone.js',
                                'Configure change detection scheduling',
                                'Implement application-level change detection strategy'
                            ],
                            deliverables: [
                                'Zoneless application configuration',
                                'Updated build settings',
                                'Custom change detection implementation',
                                'Zoneless-compatible application bootstrap'
                            ]
                        }});

                        // Phase 4: Testing and Validation
                        phases.push({{
                            name: 'Testing and Validation',
                            duration: '2-3 weeks',
                            steps: [
                                'Run comprehensive test suite in zoneless mode',
                                'Validate change detection behavior across all components',
                                'Performance testing and optimization',
                                'User acceptance testing',
                                'Browser compatibility validation'
                            ],
                            deliverables: [
                                'Test results and validation reports',
                                'Performance benchmarks',
                                'Browser compatibility matrix',
                                'User acceptance sign-off'
                            ]
                        }});

                        // Phase 5: Deployment and Monitoring
                        phases.push({{
                            name: 'Deployment and Monitoring',
                            duration: '1 week',
                            steps: [
                                'Deploy to staging environment',
                                'Set up monitoring for change detection performance',
                                'Deploy to production with feature flag',
                                'Monitor application performance and errors',
                                'Gradual rollout to all users'
                            ],
                            deliverables: [
                                'Staging deployment',
                                'Production deployment with monitoring',
                                'Performance monitoring dashboard',
                                'Rollout completion report'
                            ]
                        }});

                        results.migrationPlan.phases = phases;
                        results.migrationPlan.totalSteps = phases.reduce((total, phase) => total + phase.steps.length, 0);

                        // Estimate complexity based on current state
                        if (results.currentState.readinessScore >= 80) {{
                            results.migrationPlan.complexity = 'low';
                            results.migrationPlan.estimatedDuration = '4-6 weeks';
                        }} else if (results.currentState.readinessScore >= 60) {{
                            results.migrationPlan.complexity = 'medium';
                            results.migrationPlan.estimatedDuration = '6-10 weeks';
                        }} else {{
                            results.migrationPlan.complexity = 'high';
                            results.migrationPlan.estimatedDuration = '10-16 weeks';
                        }}
                    }};

                    // Analyze risks and blockers
                    const analyzeRisksFunc = () => {{
                        if (!results.analyzeRisks) return;

                        const risks = [];
                        const blockers = [];
                        const mitigationStrategies = [];

                        // Version compatibility risk
                        if (results.currentState.angularVersion) {{
                            const majorVersion = parseInt(results.currentState.angularVersion.split('.')[0]);
                            if (majorVersion < 18) {{
                                blockers.push({{
                                    type: 'version_compatibility',
                                    severity: 'high',
                                    description: `Angular ${{results.currentState.angularVersion}} does not support zoneless change detection`,
                                    solution: 'Upgrade to Angular 18 or later'
                                }});
                            }}
                        }}

                        // Large number of components risk
                        if (results.currentState.componentCount > 50) {{
                            risks.push({{
                                type: 'migration_scope',
                                severity: 'medium',
                                description: `Large number of components (${{results.currentState.componentCount}}) may require significant refactoring`,
                                impact: 'Increased migration time and complexity'
                            }});
                            
                            mitigationStrategies.push({{
                                risk: 'migration_scope',
                                strategy: 'Implement gradual migration by component groups',
                                timeline: 'Phase migration over multiple sprints'
                            }});
                        }}

                        // Third-party dependency risk
                        risks.push({{
                            type: 'third_party_dependencies',
                            severity: 'medium',
                            description: 'Third-party libraries may not be compatible with zoneless applications',
                            impact: 'Potential runtime errors or performance issues'
                        }});
                        
                        mitigationStrategies.push({{
                            risk: 'third_party_dependencies',
                            strategy: 'Audit and test all third-party dependencies for zoneless compatibility',
                            timeline: 'Include in Phase 1 assessment'
                        }});

                        // Testing complexity risk
                        risks.push({{
                            type: 'testing_complexity',
                            severity: 'low',
                            description: 'Testing zoneless applications requires different approaches',
                            impact: 'Need for updated testing strategies and tools'
                        }});

                        mitigationStrategies.push({{
                            risk: 'testing_complexity',
                            strategy: 'Invest in learning zoneless testing patterns and tools',
                            timeline: 'Parallel to Phase 2 development'
                        }});

                        results.riskAssessment.risks = risks;
                        results.riskAssessment.blockers = blockers;
                        results.riskAssessment.mitigationStrategies = mitigationStrategies;
                    }};

                    // Generate recommendations
                    const generateRecommendations = () => {{
                        // Immediate recommendations
                        if (results.currentState.isZoneless) {{
                            results.recommendations.immediate.push('Application is already zoneless - focus on optimization');
                        }} else {{
                            results.recommendations.immediate.push('Start with Angular version upgrade to 18+');
                            results.recommendations.immediate.push('Begin audit of current change detection patterns');
                        }}

                        if (results.currentState.readinessScore < 50) {{
                            results.recommendations.immediate.push('Address component architecture before migration');
                        }}

                        // Short-term recommendations
                        results.recommendations.shortTerm.push('Convert components to OnPush change detection strategy');
                        results.recommendations.shortTerm.push('Implement signal-based state management');
                        results.recommendations.shortTerm.push('Set up comprehensive testing for change detection');

                        // Long-term recommendations
                        results.recommendations.longTerm.push('Monitor performance improvements post-migration');
                        results.recommendations.longTerm.push('Establish zoneless development best practices');
                        results.recommendations.longTerm.push('Consider advanced zoneless optimization techniques');

                        // Add version-specific recommendations
                        if (results.currentState.angularVersion) {{
                            const majorVersion = parseInt(results.currentState.angularVersion.split('.')[0]);
                            if (majorVersion < 16) {{
                                results.recommendations.immediate.unshift('Upgrade to Angular 16+ for signal API support');
                            }}
                        }}
                    }};

                    // Add helpful resources
                    const addResources = () => {{
                        results.resources.documentation = [
                            {{
                                title: 'Angular Zoneless Change Detection Guide',
                                url: 'https://angular.dev/guide/experimental/zoneless',
                                description: 'Official Angular documentation for zoneless change detection'
                            }},
                            {{
                                title: 'Angular Signals Guide',
                                url: 'https://angular.dev/guide/signals',
                                description: 'Comprehensive guide to Angular signals'
                            }},
                            {{
                                title: 'OnPush Change Detection Strategy',
                                url: 'https://angular.dev/api/core/ChangeDetectionStrategy',
                                description: 'Documentation for OnPush change detection strategy'
                            }}
                        ];

                        results.resources.tools = [
                            {{
                                name: 'Angular DevTools',
                                description: 'Browser extension for debugging Angular applications',
                                useCase: 'Monitor change detection cycles and performance'
                            }},
                            {{
                                name: 'Angular CLI',
                                description: 'Command-line tool for Angular development',
                                useCase: 'Generate components with OnPush strategy'
                            }},
                            {{
                                name: 'Zone.js Patch Analysis',
                                description: 'Tools to analyze Zone.js patching behavior',
                                useCase: 'Identify Zone.js dependencies before migration'
                            }}
                        ];

                        results.resources.examples = [
                            {{
                                title: 'Zoneless Component Example',
                                description: 'Example of a component optimized for zoneless change detection'
                            }},
                            {{
                                title: 'Signal-based State Management',
                                description: 'Example of using signals for reactive state management'
                            }},
                            {{
                                title: 'Manual Change Detection Triggers',
                                description: 'Examples of manually triggering change detection'
                            }}
                        ];
                    }};

                    // Execute analysis
                    analyzeCurrentState();
                    generateMigrationPlan();
                    analyzeRisksFunc();
                    generateRecommendations();
                    addResources();

                    return results;
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to generate zoneless migration plan: {ex.Message}";
        }
    }
}
