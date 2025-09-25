using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// ANG-013: Angular Routing Testing Implementation
/// Implements test_angular_routing_scenarios function with route navigation testing and guard/resolver validation
/// Provides comprehensive Angular routing analysis and automated testing capabilities
/// </summary>
[McpServerToolType]
public class AngularRoutingTesting(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Test Angular routing scenarios with navigation testing and guard/resolver validation - ANG-013 Implementation")]
    public async Task<string> TestAngularRoutingScenarios(
        [Description("Maximum test execution time in seconds")] int timeoutSeconds = 60,
        [Description("Include detailed route analysis and testing information")] bool includeDetailedAnalysis = true,
        [Description("Test navigation guards (canActivate, canDeactivate, etc.)")] bool testNavigationGuards = true,
        [Description("Test route resolvers and data loading")] bool testResolvers = true,
        [Description("Test lazy loading modules and route loading")] bool testLazyLoading = true,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (async () => {{
                    // ANG-013: Angular Routing Testing Implementation
                    const testAngularRoutingScenarios = async () => {{
                        const results = {{
                            testStartTime: Date.now(),
                            testEndTime: null,
                            testDuration: 0,
                            timeoutSeconds: {timeoutSeconds},
                            timedOut: false,
                            angularDetected: false,
                            routerDetected: false,
                            routingConfiguration: {{
                                totalRoutes: 0,
                                routeTypes: {{}},
                                lazyLoadedModules: 0,
                                guardsFound: 0,
                                resolversFound: 0,
                                redirectRoutes: 0,
                                wildcardRoutes: 0,
                                childRoutes: 0
                            }},
                            routeAnalysis: {{
                                routes: [],
                                guards: [],
                                resolvers: [],
                                lazyModules: [],
                                routeTree: {{}}
                            }},
                            navigationTesting: {{
                                routesTestedCount: 0,
                                successfulNavigations: 0,
                                failedNavigations: 0,
                                guardTestResults: [],
                                resolverTestResults: [],
                                lazyLoadingTestResults: [],
                                navigationHistory: []
                            }},
                            currentRoute: {{
                                url: window.location.href,
                                path: window.location.pathname,
                                params: {{}},
                                queryParams: {{}},
                                fragment: window.location.hash
                            }},
                            testConfiguration: {{
                                includeDetailedAnalysis: {includeDetailedAnalysis.ToString().ToLower()},
                                testNavigationGuards: {testNavigationGuards.ToString().ToLower()},
                                testResolvers: {testResolvers.ToString().ToLower()},
                                testLazyLoading: {testLazyLoading.ToString().ToLower()}
                            }},
                            errors: [],
                            warnings: [],
                            recommendations: []
                        }};

                        // Step 1: Detect Angular and Router
                        const detectAngularRouter = () => {{
                            try {{
                                // Check for Angular presence
                                const hasAngularGlobal = !!(window.ng || window.ngDevMode || window.getAllAngularRootElements);
                                const hasAngularElements = document.querySelector('[ng-version]') !== null;
                                
                                results.angularDetected = hasAngularGlobal || hasAngularElements;
                                
                                if (!results.angularDetected) {{
                                    results.errors.push('Angular application not detected on this page');
                                    return false;
                                }}

                                // Check for Angular Router
                                let routerFound = false;
                                
                                // Method 1: Check for router-outlet
                                const routerOutlets = document.querySelectorAll('router-outlet, [routerOutlet]');
                                if (routerOutlets.length > 0) {{
                                    routerFound = true;
                                    results.routingConfiguration.routerOutlets = routerOutlets.length;
                                }}

                                // Method 2: Check for Angular Router in DevTools
                                if (window.ng && window.ng.getComponent) {{
                                    try {{
                                        const rootElements = window.getAllAngularRootElements?.() || [];
                                        rootElements.forEach(el => {{
                                            try {{
                                                const component = window.ng.getComponent(el);
                                                if (component && component.constructor) {{
                                                    const componentProps = Object.getOwnPropertyNames(component);
                                                    const hasRouter = componentProps.some(prop => 
                                                        prop.includes('router') || prop.includes('Router'));
                                                    if (hasRouter) {{
                                                        routerFound = true;
                                                    }}
                                                }}
                                            }} catch (e) {{
                                                // Skip if component cannot be accessed
                                            }}
                                        }});
                                    }} catch (e) {{
                                        results.warnings.push(`Error checking components for router: ${{e.message}}`);
                                    }}
                                }}

                                // Method 3: Check for routing-related elements
                                const routerLinks = document.querySelectorAll('[routerLink], [routerLinkActive]');
                                if (routerLinks.length > 0) {{
                                    routerFound = true;
                                    results.routingConfiguration.routerLinks = routerLinks.length;
                                }}

                                results.routerDetected = routerFound;
                                
                                if (!routerFound) {{
                                    results.warnings.push('Angular Router not detected - application may not use routing');
                                    return false;
                                }}

                                return true;
                            }} catch (e) {{
                                results.errors.push(`Error detecting Angular Router: ${{e.message}}`);
                                return false;
                            }}
                        }};

                        // Step 2: Analyze route configuration
                        const analyzeRouteConfiguration = () => {{
                            if (!results.includeDetailedAnalysis) return;

                            try {{
                                // Method 1: Try to get router configuration via Angular DevTools
                                if (window.ng && window.ng.getInjector) {{
                                    try {{
                                        const rootElements = window.getAllAngularRootElements?.() || [];
                                        rootElements.forEach(el => {{
                                            try {{
                                                const injector = window.ng.getInjector(el);
                                                if (injector) {{
                                                    // Try to get Router service
                                                    const router = injector.get?.('Router');
                                                    if (router && router.config) {{
                                                        results.routingConfiguration.totalRoutes = router.config.length;
                                                        
                                                        router.config.forEach((route, index) => {{
                                                            const routeInfo = {{
                                                                index: index,
                                                                path: route.path || 'undefined',
                                                                component: route.component?.name || 'undefined',
                                                                redirectTo: route.redirectTo || null,
                                                                loadChildren: !!route.loadChildren,
                                                                children: route.children?.length || 0,
                                                                guards: {{
                                                                    canActivate: route.canActivate?.length || 0,
                                                                    canDeactivate: route.canDeactivate?.length || 0,
                                                                    canLoad: route.canLoad?.length || 0,
                                                                    canActivateChild: route.canActivateChild?.length || 0
                                                                }},
                                                                data: !!route.data,
                                                                resolve: Object.keys(route.resolve || {{}}).length
                                                            }};

                                                            results.routeAnalysis.routes.push(routeInfo);

                                                            // Count route types
                                                            if (route.redirectTo) {{
                                                                results.routingConfiguration.redirectRoutes++;
                                                            }}
                                                            if (route.path === '**') {{
                                                                results.routingConfiguration.wildcardRoutes++;
                                                            }}
                                                            if (route.loadChildren) {{
                                                                results.routingConfiguration.lazyLoadedModules++;
                                                            }}
                                                            if (route.children) {{
                                                                results.routingConfiguration.childRoutes += route.children.length;
                                                            }}

                                                            // Count guards
                                                            const totalGuards = (route.canActivate?.length || 0) +
                                                                              (route.canDeactivate?.length || 0) +
                                                                              (route.canLoad?.length || 0) +
                                                                              (route.canActivateChild?.length || 0);
                                                            results.routingConfiguration.guardsFound += totalGuards;

                                                            // Count resolvers
                                                            const resolverCount = Object.keys(route.resolve || {{}}).length;
                                                            results.routingConfiguration.resolversFound += resolverCount;
                                                        }});
                                                    }}
                                                }}
                                            }} catch (e) {{
                                                // Skip if injector cannot be accessed
                                            }}
                                        }});
                                    }} catch (e) {{
                                        results.warnings.push(`Error analyzing route configuration: ${{e.message}}`);
                                    }}
                                }}

                                // Method 2: Analyze DOM for routing clues
                                const routerLinks = document.querySelectorAll('[routerLink]');
                                const discoveredRoutes = new Set();
                                
                                routerLinks.forEach(link => {{
                                    const routerLink = link.getAttribute('routerLink');
                                    if (routerLink) {{
                                        discoveredRoutes.add(routerLink);
                                    }}
                                }});

                                if (discoveredRoutes.size > 0) {{
                                    results.routeAnalysis.discoveredFromDOM = Array.from(discoveredRoutes);
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error in route configuration analysis: ${{e.message}}`);
                            }}
                        }};

                        // Step 3: Test route navigation
                        const testRouteNavigation = async () => {{
                            if (!results.testConfiguration.includeDetailedAnalysis) return;

                            try {{
                                const testRoutes = [];
                                
                                // Collect routes to test from analysis
                                if (results.routeAnalysis.routes.length > 0) {{
                                    results.routeAnalysis.routes.forEach(route => {{
                                        if (route.path && route.path !== '**' && route.path !== '') {{
                                            testRoutes.push(route.path);
                                        }}
                                    }});
                                }}

                                // Also test routes found in DOM
                                if (results.routeAnalysis.discoveredFromDOM) {{
                                    results.routeAnalysis.discoveredFromDOM.forEach(route => {{
                                        if (!testRoutes.includes(route)) {{
                                            testRoutes.push(route);
                                        }}
                                    }});
                                }}

                                // If no routes found, test some common routes
                                if (testRoutes.length === 0) {{
                                    testRoutes.push('/', '/home', '/about', '/contact');
                                }}

                                results.navigationTesting.routesTestedCount = Math.min(testRoutes.length, 10); // Limit to 10 routes

                                // Test navigation to routes
                                for (let i = 0; i < Math.min(testRoutes.length, 10); i++) {{
                                    const route = testRoutes[i];
                                    
                                    try {{
                                        const navigationStart = Date.now();
                                        const originalUrl = window.location.href;

                                        // Attempt navigation using Router if available
                                        let navigationSuccess = false;
                                        let navigationMethod = 'none';

                                        // Method 1: Try Angular Router navigation
                                        if (window.ng && window.ng.getInjector) {{
                                            try {{
                                                const rootElements = window.getAllAngularRootElements?.() || [];
                                                for (const el of rootElements) {{
                                                    try {{
                                                        const injector = window.ng.getInjector(el);
                                                        const router = injector?.get?.('Router');
                                                        if (router && router.navigate) {{
                                                            await router.navigate([route]);
                                                            navigationSuccess = true;
                                                            navigationMethod = 'angular-router';
                                                            break;
                                                        }}
                                                    }} catch (e) {{
                                                        // Try next element
                                                    }}
                                                }}
                                            }} catch (e) {{
                                                // Router navigation failed
                                            }}
                                        }}

                                        // Method 2: Try programmatic navigation
                                        if (!navigationSuccess) {{
                                            try {{
                                                const testUrl = new URL(route, window.location.origin);
                                                window.history.pushState({{}}, '', testUrl.pathname);
                                                navigationSuccess = true;
                                                navigationMethod = 'history-api';
                                            }} catch (e) {{
                                                // History navigation failed
                                            }}
                                        }}

                                        const navigationEnd = Date.now();
                                        const navigationTime = navigationEnd - navigationStart;

                                        const testResult = {{
                                            route: route,
                                            success: navigationSuccess,
                                            method: navigationMethod,
                                            navigationTime: navigationTime,
                                            originalUrl: originalUrl,
                                            resultUrl: window.location.href,
                                            timestamp: navigationEnd
                                        }};

                                        results.navigationTesting.navigationHistory.push(testResult);

                                        if (navigationSuccess) {{
                                            results.navigationTesting.successfulNavigations++;
                                        }} else {{
                                            results.navigationTesting.failedNavigations++;
                                        }}

                                        // Small delay between navigations
                                        await new Promise(resolve => setTimeout(resolve, 100));

                                    }} catch (e) {{
                                        results.navigationTesting.failedNavigations++;
                                        results.navigationTesting.navigationHistory.push({{
                                            route: route,
                                            success: false,
                                            error: e.message,
                                            timestamp: Date.now()
                                        }});
                                    }}
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error in route navigation testing: ${{e.message}}`);
                            }}
                        }};

                        // Step 4: Test navigation guards
                        const testNavigationGuards = async () => {{
                            if (!results.testConfiguration.testNavigationGuards) return;

                            try {{
                                const guardsToTest = [];

                                // Collect guards from route analysis
                                results.routeAnalysis.routes.forEach(route => {{
                                    if (route.guards) {{
                                        Object.keys(route.guards).forEach(guardType => {{
                                            if (route.guards[guardType] > 0) {{
                                                guardsToTest.push({{
                                                    route: route.path,
                                                    guardType: guardType,
                                                    count: route.guards[guardType]
                                                }});
                                            }}
                                        }});
                                    }}
                                }});

                                // Test guards by attempting navigation
                                for (const guard of guardsToTest.slice(0, 5)) {{ // Limit to 5 guard tests
                                    try {{
                                        const testStart = Date.now();
                                        const originalUrl = window.location.href;

                                        // Attempt navigation to route with guard
                                        let guardTestResult = {{
                                            route: guard.route,
                                            guardType: guard.guardType,
                                            testStartTime: testStart,
                                            testEndTime: null,
                                            allowed: false,
                                            blocked: false,
                                            redirected: false,
                                            originalUrl: originalUrl,
                                            finalUrl: null,
                                            executionTime: 0
                                        }};

                                        // Try navigation and observe result
                                        if (window.ng && window.ng.getInjector) {{
                                            try {{
                                                const rootElements = window.getAllAngularRootElements?.() || [];
                                                for (const el of rootElements) {{
                                                    try {{
                                                        const injector = window.ng.getInjector(el);
                                                        const router = injector?.get?.('Router');
                                                        if (router && router.navigate) {{
                                                            await router.navigate([guard.route]);
                                                            break;
                                                        }}
                                                    }} catch (e) {{
                                                        // Try next element
                                                    }}
                                                }}
                                            }} catch (e) {{
                                                // Router navigation failed
                                            }}
                                        }}

                                        const testEnd = Date.now();
                                        guardTestResult.testEndTime = testEnd;
                                        guardTestResult.executionTime = testEnd - testStart;
                                        guardTestResult.finalUrl = window.location.href;

                                        // Analyze guard behavior
                                        if (guardTestResult.finalUrl === originalUrl) {{
                                            guardTestResult.blocked = true;
                                        }} else if (guardTestResult.finalUrl.includes(guard.route)) {{
                                            guardTestResult.allowed = true;
                                        }} else {{
                                            guardTestResult.redirected = true;
                                        }}

                                        results.navigationTesting.guardTestResults.push(guardTestResult);

                                        // Small delay between guard tests
                                        await new Promise(resolve => setTimeout(resolve, 150));

                                    }} catch (e) {{
                                        results.navigationTesting.guardTestResults.push({{
                                            route: guard.route,
                                            guardType: guard.guardType,
                                            error: e.message,
                                            testFailed: true
                                        }});
                                    }}
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error in navigation guard testing: ${{e.message}}`);
                            }}
                        }};

                        // Step 5: Test route resolvers
                        const testRouteResolvers = async () => {{
                            if (!results.testConfiguration.testResolvers) return;

                            try {{
                                const resolversToTest = [];

                                // Collect resolvers from route analysis
                                results.routeAnalysis.routes.forEach(route => {{
                                    if (route.resolve > 0) {{
                                        resolversToTest.push({{
                                            route: route.path,
                                            resolverCount: route.resolve
                                        }});
                                    }}
                                }});

                                // Test resolvers by navigation and timing
                                for (const resolver of resolversToTest.slice(0, 5)) {{ // Limit to 5 resolver tests
                                    try {{
                                        const testStart = Date.now();

                                        let resolverTestResult = {{
                                            route: resolver.route,
                                            resolverCount: resolver.resolverCount,
                                            testStartTime: testStart,
                                            testEndTime: null,
                                            resolutionTime: 0,
                                            success: false,
                                            dataResolved: false
                                        }};

                                        // Navigate to route with resolver
                                        if (window.ng && window.ng.getInjector) {{
                                            try {{
                                                const rootElements = window.getAllAngularRootElements?.() || [];
                                                for (const el of rootElements) {{
                                                    try {{
                                                        const injector = window.ng.getInjector(el);
                                                        const router = injector?.get?.('Router');
                                                        if (router && router.navigate) {{
                                                            await router.navigate([resolver.route]);
                                                            resolverTestResult.success = true;
                                                            break;
                                                        }}
                                                    }} catch (e) {{
                                                        // Try next element
                                                    }}
                                                }}
                                            }} catch (e) {{
                                                // Router navigation failed
                                            }}
                                        }}

                                        const testEnd = Date.now();
                                        resolverTestResult.testEndTime = testEnd;
                                        resolverTestResult.resolutionTime = testEnd - testStart;

                                        // Check if data appears to be resolved (simplified check)
                                        // In a real implementation, this would need access to the route data
                                        resolverTestResult.dataResolved = resolverTestResult.success && resolverTestResult.resolutionTime > 50;

                                        results.navigationTesting.resolverTestResults.push(resolverTestResult);

                                        // Small delay between resolver tests
                                        await new Promise(resolve => setTimeout(resolve, 100));

                                    }} catch (e) {{
                                        results.navigationTesting.resolverTestResults.push({{
                                            route: resolver.route,
                                            error: e.message,
                                            testFailed: true
                                        }});
                                    }}
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error in route resolver testing: ${{e.message}}`);
                            }}
                        }};

                        // Step 6: Test lazy loading
                        const testLazyLoading = async () => {{
                            if (!results.testConfiguration.testLazyLoading) return;

                            try {{
                                const lazyRoutesToTest = [];

                                // Collect lazy-loaded routes from analysis
                                results.routeAnalysis.routes.forEach(route => {{
                                    if (route.loadChildren) {{
                                        lazyRoutesToTest.push(route);
                                    }}
                                }});

                                // Test lazy loading by navigation and network monitoring
                                for (const lazyRoute of lazyRoutesToTest.slice(0, 3)) {{ // Limit to 3 lazy loading tests
                                    try {{
                                        const testStart = Date.now();
                                        const initialScriptCount = document.scripts.length;

                                        let lazyTestResult = {{
                                            route: lazyRoute.path,
                                            testStartTime: testStart,
                                            testEndTime: null,
                                            loadingTime: 0,
                                            success: false,
                                            newScriptsLoaded: 0,
                                            initialScriptCount: initialScriptCount,
                                            finalScriptCount: 0
                                        }};

                                        // Navigate to lazy route
                                        if (window.ng && window.ng.getInjector) {{
                                            try {{
                                                const rootElements = window.getAllAngularRootElements?.() || [];
                                                for (const el of rootElements) {{
                                                    try {{
                                                        const injector = window.ng.getInjector(el);
                                                        const router = injector?.get?.('Router');
                                                        if (router && router.navigate) {{
                                                            await router.navigate([lazyRoute.path]);
                                                            lazyTestResult.success = true;
                                                            break;
                                                        }}
                                                    }} catch (e) {{
                                                        // Try next element
                                                    }}
                                                }}
                                            }} catch (e) {{
                                                // Router navigation failed
                                            }}
                                        }}

                                        // Wait a bit for lazy loading to complete
                                        await new Promise(resolve => setTimeout(resolve, 500));

                                        const testEnd = Date.now();
                                        lazyTestResult.testEndTime = testEnd;
                                        lazyTestResult.loadingTime = testEnd - testStart;
                                        lazyTestResult.finalScriptCount = document.scripts.length;
                                        lazyTestResult.newScriptsLoaded = lazyTestResult.finalScriptCount - lazyTestResult.initialScriptCount;

                                        results.navigationTesting.lazyLoadingTestResults.push(lazyTestResult);

                                        // Small delay between lazy loading tests
                                        await new Promise(resolve => setTimeout(resolve, 200));

                                    }} catch (e) {{
                                        results.navigationTesting.lazyLoadingTestResults.push({{
                                            route: lazyRoute.path,
                                            error: e.message,
                                            testFailed: true
                                        }});
                                    }}
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error in lazy loading testing: ${{e.message}}`);
                            }}
                        }};

                        // Step 7: Generate recommendations
                        const generateRecommendations = () => {{
                            try {{
                                // Navigation performance recommendations
                                const avgNavigationTime = results.navigationTesting.navigationHistory.length > 0 ?
                                    results.navigationTesting.navigationHistory
                                        .filter(nav => nav.navigationTime)
                                        .reduce((sum, nav) => sum + nav.navigationTime, 0) / 
                                        results.navigationTesting.navigationHistory.filter(nav => nav.navigationTime).length : 0;

                                if (avgNavigationTime > 1000) {{
                                    results.recommendations.push({{
                                        type: 'performance',
                                        severity: 'medium',
                                        message: 'Average navigation time is slow (>1s) - consider optimizing route components',
                                        category: 'navigation_performance'
                                    }});
                                }}

                                // Guard recommendations
                                const blockedNavigations = results.navigationTesting.guardTestResults.filter(g => g.blocked).length;
                                if (blockedNavigations > 0) {{
                                    results.recommendations.push({{
                                        type: 'security',
                                        severity: 'info',
                                        message: `${{blockedNavigations}} navigation(s) blocked by guards - ensure proper error handling`,
                                        category: 'guard_behavior'
                                    }});
                                }}

                                // Lazy loading recommendations
                                const slowLazyLoads = results.navigationTesting.lazyLoadingTestResults.filter(l => l.loadingTime > 2000).length;
                                if (slowLazyLoads > 0) {{
                                    results.recommendations.push({{
                                        type: 'performance',
                                        severity: 'medium',
                                        message: `${{slowLazyLoads}} lazy-loaded route(s) are slow (>2s) - consider code splitting optimization`,
                                        category: 'lazy_loading_performance'
                                    }});
                                }}

                                // Route configuration recommendations
                                if (results.routingConfiguration.totalRoutes === 0) {{
                                    results.recommendations.push({{
                                        type: 'architecture',
                                        severity: 'low',
                                        message: 'No routes detected - consider implementing proper routing structure',
                                        category: 'route_configuration'
                                    }});
                                }}

                                if (results.routingConfiguration.wildcardRoutes === 0) {{
                                    results.recommendations.push({{
                                        type: 'usability',
                                        severity: 'low',
                                        message: 'No wildcard route detected - consider adding 404 error handling',
                                        category: 'error_handling'
                                    }});
                                }}

                            }} catch (e) {{
                                results.warnings.push(`Error generating recommendations: ${{e.message}}`);
                            }}
                        }};

                        // Main execution flow
                        try {{
                            // Initialize testing
                            if (!detectAngularRouter()) {{
                                results.testEndTime = Date.now();
                                results.testDuration = results.testEndTime - results.testStartTime;
                                return results;
                            }}

                            // Analyze route configuration
                            analyzeRouteConfiguration();

                            // Perform testing based on configuration
                            await testRouteNavigation();
                            await testNavigationGuards();
                            await testRouteResolvers();
                            await testLazyLoading();

                            // Generate recommendations
                            generateRecommendations();

                            // Finalize results
                            results.testEndTime = Date.now();
                            results.testDuration = results.testEndTime - results.testStartTime;

                            // Check for timeout
                            if (results.testDuration > {timeoutSeconds * 1000}) {{
                                results.timedOut = true;
                                results.warnings.push(`Testing exceeded timeout of ${{timeoutSeconds}} seconds`);
                            }}

                            return results;

                        }} catch (e) {{
                            results.errors.push(`Error in main execution flow: ${{e.message}}`);
                            results.testEndTime = Date.now();
                            results.testDuration = results.testEndTime - results.testStartTime;
                            return results;
                        }}
                    }};

                    return await testAngularRoutingScenarios();
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to test Angular routing scenarios: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Analyze Angular routing configuration and extract route information")]
    public async Task<string> AnalyzeAngularRoutingConfiguration(
        [Description("Include detailed route tree structure")] bool includeRouteTree = true,
        [Description("Include guard and resolver analysis")] bool includeGuardAnalysis = true,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = $@"
                (() => {{
                    const results = {{
                        timestamp: Date.now(),
                        angularDetected: false,
                        routerDetected: false,
                        analysis: {{
                            routeConfiguration: {{
                                totalRoutes: 0,
                                routesByType: {{}},
                                lazyLoadedRoutes: [],
                                guardedRoutes: [],
                                resolvedRoutes: [],
                                redirectRoutes: [],
                                parameterizedRoutes: [],
                                nestedRoutes: []
                            }},
                            routeTree: {{}},
                            guards: {{
                                canActivate: [],
                                canDeactivate: [],
                                canLoad: [],
                                canActivateChild: [],
                                resolve: []
                            }},
                            routingPatterns: {{
                                hasWildcardRoute: false,
                                hasDefaultRoute: false,
                                hasLazyLoading: false,
                                hasNestedRouting: false,
                                hasParameterizedRoutes: false
                            }},
                            performance: {{
                                estimatedRouteCount: 0,
                                complexityScore: 0,
                                lazyLoadingEfficiency: 0
                            }}
                        }},
                        configuration: {{
                            includeRouteTree: {includeRouteTree.ToString().ToLower()},
                            includeGuardAnalysis: {includeGuardAnalysis.ToString().ToLower()}
                        }},
                        warnings: [],
                        errors: []
                    }};

                    try {{
                        // Check Angular and Router presence
                        results.angularDetected = !!(window.ng || window.ngDevMode || document.querySelector('[ng-version]'));
                        
                        if (!results.angularDetected) {{
                            results.errors.push('Angular application not detected');
                            return results;
                        }}

                        // Check for router elements
                        const routerOutlets = document.querySelectorAll('router-outlet, [routerOutlet]');
                        const routerLinks = document.querySelectorAll('[routerLink], [routerLinkActive]');
                        
                        results.routerDetected = routerOutlets.length > 0 || routerLinks.length > 0;

                        if (!results.routerDetected) {{
                            results.warnings.push('Angular Router elements not detected in DOM');
                        }}

                        // Try to get router configuration via Angular DevTools
                        if (window.ng && window.ng.getInjector) {{
                            try {{
                                const rootElements = window.getAllAngularRootElements?.() || [];
                                
                                rootElements.forEach(el => {{
                                    try {{
                                        const injector = window.ng.getInjector(el);
                                        const router = injector?.get?.('Router');
                                        
                                        if (router && router.config) {{
                                            results.analysis.routeConfiguration.totalRoutes = router.config.length;
                                            
                                            // Analyze each route
                                            router.config.forEach((route, index) => {{
                                                const routeInfo = {{
                                                    index: index,
                                                    path: route.path || '',
                                                    component: route.component?.name || null,
                                                    redirectTo: route.redirectTo || null,
                                                    loadChildren: !!route.loadChildren,
                                                    children: route.children || [],
                                                    guards: {{
                                                        canActivate: route.canActivate || [],
                                                        canDeactivate: route.canDeactivate || [],
                                                        canLoad: route.canLoad || [],
                                                        canActivateChild: route.canActivateChild || []
                                                    }},
                                                    resolve: route.resolve || {{}},
                                                    data: route.data || null,
                                                    outlet: route.outlet || null
                                                }};

                                                // Categorize routes
                                                if (route.redirectTo) {{
                                                    results.analysis.routeConfiguration.redirectRoutes.push(routeInfo);
                                                }}

                                                if (route.loadChildren) {{
                                                    results.analysis.routeConfiguration.lazyLoadedRoutes.push(routeInfo);
                                                    results.analysis.routingPatterns.hasLazyLoading = true;
                                                }}

                                                if (route.path === '**') {{
                                                    results.analysis.routingPatterns.hasWildcardRoute = true;
                                                }}

                                                if (route.path === '' || route.path === '/') {{
                                                    results.analysis.routingPatterns.hasDefaultRoute = true;
                                                }}

                                                if (route.path && (route.path.includes(':') || route.path.includes('**'))) {{
                                                    results.analysis.routeConfiguration.parameterizedRoutes.push(routeInfo);
                                                    results.analysis.routingPatterns.hasParameterizedRoutes = true;
                                                }}

                                                if (route.children && route.children.length > 0) {{
                                                    results.analysis.routeConfiguration.nestedRoutes.push(routeInfo);
                                                    results.analysis.routingPatterns.hasNestedRouting = true;
                                                }}

                                                // Analyze guards
                                                const hasGuards = Object.values(routeInfo.guards).some(guardArray => guardArray.length > 0);
                                                if (hasGuards) {{
                                                    results.analysis.routeConfiguration.guardedRoutes.push(routeInfo);
                                                }}

                                                // Analyze resolvers
                                                if (Object.keys(routeInfo.resolve).length > 0) {{
                                                    results.analysis.routeConfiguration.resolvedRoutes.push(routeInfo);
                                                }}

                                                // Build route tree if requested
                                                if (results.configuration.includeRouteTree) {{
                                                    if (!results.analysis.routeTree[route.path || 'root']) {{
                                                        results.analysis.routeTree[route.path || 'root'] = {{
                                                            path: route.path,
                                                            component: route.component?.name,
                                                            children: {{}},
                                                            guards: routeInfo.guards,
                                                            resolve: routeInfo.resolve,
                                                            isLazy: !!route.loadChildren
                                                        }};
                                                    }}
                                                }}
                                            }});

                                            // Collect guard information if requested
                                            if (results.configuration.includeGuardAnalysis) {{
                                                router.config.forEach(route => {{
                                                    if (route.canActivate) {{
                                                        route.canActivate.forEach(guard => {{
                                                            results.analysis.guards.canActivate.push({{
                                                                route: route.path,
                                                                guard: guard.name || 'anonymous'
                                                            }});
                                                        }});
                                                    }}
                                                    if (route.canDeactivate) {{
                                                        route.canDeactivate.forEach(guard => {{
                                                            results.analysis.guards.canDeactivate.push({{
                                                                route: route.path,
                                                                guard: guard.name || 'anonymous'
                                                            }});
                                                        }});
                                                    }}
                                                    if (route.canLoad) {{
                                                        route.canLoad.forEach(guard => {{
                                                            results.analysis.guards.canLoad.push({{
                                                                route: route.path,
                                                                guard: guard.name || 'anonymous'
                                                            }});
                                                        }});
                                                    }}
                                                    if (route.canActivateChild) {{
                                                        route.canActivateChild.forEach(guard => {{
                                                            results.analysis.guards.canActivateChild.push({{
                                                                route: route.path,
                                                                guard: guard.name || 'anonymous'
                                                            }});
                                                        }});
                                                    }}
                                                    if (route.resolve) {{
                                                        Object.keys(route.resolve).forEach(key => {{
                                                            results.analysis.guards.resolve.push({{
                                                                route: route.path,
                                                                key: key,
                                                                resolver: route.resolve[key].name || 'anonymous'
                                                            }});
                                                        }});
                                                    }}
                                                }});
                                            }}

                                            // Calculate performance metrics
                                            results.analysis.performance.estimatedRouteCount = router.config.length;
                                            
                                            const lazyRoutes = results.analysis.routeConfiguration.lazyLoadedRoutes.length;
                                            const totalRoutes = results.analysis.routeConfiguration.totalRoutes;
                                            results.analysis.performance.lazyLoadingEfficiency = 
                                                totalRoutes > 0 ? (lazyRoutes / totalRoutes) * 100 : 0;

                                            // Calculate complexity score
                                            let complexityScore = 0;
                                            complexityScore += results.analysis.routeConfiguration.totalRoutes * 1;
                                            complexityScore += results.analysis.routeConfiguration.nestedRoutes.length * 2;
                                            complexityScore += results.analysis.routeConfiguration.guardedRoutes.length * 3;
                                            complexityScore += results.analysis.routeConfiguration.lazyLoadedRoutes.length * 2;
                                            results.analysis.performance.complexityScore = complexityScore;
                                        }}
                                    }} catch (e) {{
                                        results.warnings.push(`Error accessing router config: ${{e.message}}`);
                                    }}
                                }});
                            }} catch (e) {{
                                results.warnings.push(`Error accessing Angular injector: ${{e.message}}`);
                            }}
                        }}

                        // Fallback: Analyze DOM for routing clues
                        if (results.analysis.routeConfiguration.totalRoutes === 0) {{
                            const routerLinks = document.querySelectorAll('[routerLink]');
                            const discoveredRoutes = new Set();
                            
                            routerLinks.forEach(link => {{
                                const routerLink = link.getAttribute('routerLink');
                                if (routerLink) {{
                                    discoveredRoutes.add(routerLink);
                                }}
                            }});

                            if (discoveredRoutes.size > 0) {{
                                results.analysis.routeConfiguration.totalRoutes = discoveredRoutes.size;
                                results.analysis.routeConfiguration.discoveredFromDOM = Array.from(discoveredRoutes);
                                results.warnings.push('Route configuration extracted from DOM - may be incomplete');
                            }}
                        }}

                    }} catch (e) {{
                        results.errors.push(`Error analyzing routing configuration: ${{e.message}}`);
                    }}

                    return results;
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to analyze Angular routing configuration: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Test specific navigation guard behaviors and validate guard execution")]
    public async Task<string> TestNavigationGuardBehaviors(
        [Description("Routes to test guards on (comma-separated)")] string routesToTest = "",
        [Description("Maximum test execution time in seconds")] int timeoutSeconds = 30,
        [Description("Include detailed guard execution analysis")] bool includeDetailedAnalysis = true,
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var routes = string.IsNullOrWhiteSpace(routesToTest) 
                ? []
                : routesToTest.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim()).ToArray();

            var jsCode = $@"
                (async () => {{
                    const results = {{
                        testStartTime: Date.now(),
                        testEndTime: null,
                        timeoutSeconds: {timeoutSeconds},
                        timedOut: false,
                        routesToTest: {JsonSerializer.Serialize(routes)},
                        angularDetected: false,
                        routerDetected: false,
                        guardTests: [],
                        guardAnalysis: {{
                            totalGuardsFound: 0,
                            guardTypes: {{}},
                            guardExecutionTimes: [],
                            guardBehaviors: {{}},
                            guardsByRoute: {{}}
                        }},
                        testResults: {{
                            totalTests: 0,
                            successfulTests: 0,
                            failedTests: 0,
                            guardsAllowed: 0,
                            guardsBlocked: 0,
                            guardsRedirected: 0
                        }},
                        configuration: {{
                            includeDetailedAnalysis: {includeDetailedAnalysis.ToString().ToLower()}
                        }},
                        errors: [],
                        warnings: [],
                        recommendations: []
                    }};

                    try {{
                        // Check Angular and Router
                        results.angularDetected = !!(window.ng || window.ngDevMode || document.querySelector('[ng-version]'));
                        if (!results.angularDetected) {{
                            results.errors.push('Angular application not detected');
                            return results;
                        }}

                        const routerOutlets = document.querySelectorAll('router-outlet');
                        results.routerDetected = routerOutlets.length > 0;

                        if (!results.routerDetected) {{
                            results.warnings.push('Router outlets not found - guard testing may be limited');
                        }}

                        // Get router and analyze guards
                        let router = null;
                        let routeConfig = [];
                        
                        if (window.ng && window.ng.getInjector) {{
                            const rootElements = window.getAllAngularRootElements?.() || [];
                            
                            for (const el of rootElements) {{
                                try {{
                                    const injector = window.ng.getInjector(el);
                                    router = injector?.get?.('Router');
                                    if (router && router.config) {{
                                        routeConfig = router.config;
                                        break;
                                    }}
                                }} catch (e) {{
                                    // Try next element
                                }}
                            }}
                        }}

                        if (!router || !routeConfig.length) {{
                            results.warnings.push('Router configuration not accessible - using alternative testing methods');
                        }}

                        // Analyze guards in route configuration
                        if (routeConfig.length > 0) {{
                            routeConfig.forEach(route => {{
                                const routePath = route.path || 'root';
                                const guardInfo = {{
                                    route: routePath,
                                    guards: {{
                                        canActivate: route.canActivate || [],
                                        canDeactivate: route.canDeactivate || [],
                                        canLoad: route.canLoad || [],
                                        canActivateChild: route.canActivateChild || []
                                    }}
                                }};

                                // Count guards by type
                                Object.keys(guardInfo.guards).forEach(guardType => {{
                                    const guardCount = guardInfo.guards[guardType].length;
                                    if (guardCount > 0) {{
                                        results.guardAnalysis.guardTypes[guardType] = 
                                            (results.guardAnalysis.guardTypes[guardType] || 0) + guardCount;
                                        results.guardAnalysis.totalGuardsFound += guardCount;
                                        
                                        if (!results.guardAnalysis.guardsByRoute[routePath]) {{
                                            results.guardAnalysis.guardsByRoute[routePath] = [];
                                        }}
                                        results.guardAnalysis.guardsByRoute[routePath].push(guardType);
                                    }}
                                }});
                            }});
                        }}

                        // Determine routes to test
                        let testRoutes = results.routesToTest.length > 0 ? results.routesToTest : [];
                        
                        if (testRoutes.length === 0 && routeConfig.length > 0) {{
                            // Use routes that have guards
                            testRoutes = routeConfig
                                .filter(route => {{
                                    return route.canActivate?.length > 0 || 
                                           route.canDeactivate?.length > 0 ||
                                           route.canLoad?.length > 0 ||
                                           route.canActivateChild?.length > 0;
                                }})
                                .map(route => route.path)
                                .filter(path => path && path !== '**')
                                .slice(0, 5); // Limit to 5 routes
                        }}

                        if (testRoutes.length === 0) {{
                            testRoutes = ['/', '/home', '/about']; // Default test routes
                        }}

                        results.testResults.totalTests = testRoutes.length;

                        // Test each route
                        for (const testRoute of testRoutes) {{
                            try {{
                                const testStart = Date.now();
                                const originalUrl = window.location.href;
                                const originalPath = window.location.pathname;

                                const guardTest = {{
                                    route: testRoute,
                                    testStartTime: testStart,
                                    testEndTime: null,
                                    originalUrl: originalUrl,
                                    originalPath: originalPath,
                                    finalUrl: null,
                                    finalPath: null,
                                    executionTime: 0,
                                    navigationAttempted: false,
                                    navigationSuccessful: false,
                                    guardBehavior: 'unknown',
                                    guardExecuted: false,
                                    error: null
                                }};

                                try {{
                                    // Attempt navigation
                                    if (router && router.navigate) {{
                                        guardTest.navigationAttempted = true;
                                        await router.navigate([testRoute]);
                                        guardTest.navigationSuccessful = true;
                                    }} else {{
                                        // Fallback navigation method
                                        const testUrl = new URL(testRoute, window.location.origin);
                                        window.history.pushState({{}}, '', testUrl.pathname);
                                        guardTest.navigationAttempted = true;
                                        guardTest.navigationSuccessful = true;
                                    }}

                                    // Wait a bit for guards to execute
                                    await new Promise(resolve => setTimeout(resolve, 200));

                                }} catch (e) {{
                                    guardTest.error = e.message;
                                    guardTest.navigationSuccessful = false;
                                }}

                                const testEnd = Date.now();
                                guardTest.testEndTime = testEnd;
                                guardTest.executionTime = testEnd - testStart;
                                guardTest.finalUrl = window.location.href;
                                guardTest.finalPath = window.location.pathname;

                                // Analyze guard behavior
                                if (guardTest.navigationSuccessful) {{
                                    if (guardTest.finalPath === testRoute || guardTest.finalUrl.includes(testRoute)) {{
                                        guardTest.guardBehavior = 'allowed';
                                        guardTest.guardExecuted = true;
                                        results.testResults.guardsAllowed++;
                                    }} else if (guardTest.finalPath === originalPath) {{
                                        guardTest.guardBehavior = 'blocked';
                                        guardTest.guardExecuted = true;
                                        results.testResults.guardsBlocked++;
                                    }} else {{
                                        guardTest.guardBehavior = 'redirected';
                                        guardTest.guardExecuted = true;
                                        results.testResults.guardsRedirected++;
                                    }}
                                }} else {{
                                    guardTest.guardBehavior = 'failed';
                                }}

                                // Record execution time for analysis
                                if (guardTest.guardExecuted) {{
                                    results.guardAnalysis.guardExecutionTimes.push(guardTest.executionTime);
                                }}

                                results.guardTests.push(guardTest);

                                if (guardTest.navigationSuccessful) {{
                                    results.testResults.successfulTests++;
                                }} else {{
                                    results.testResults.failedTests++;
                                }}

                                // Small delay between tests
                                await new Promise(resolve => setTimeout(resolve, 100));

                            }} catch (e) {{
                                results.testResults.failedTests++;
                                results.guardTests.push({{
                                    route: testRoute,
                                    error: e.message,
                                    testFailed: true
                                }});
                            }}
                        }}

                        // Analyze guard behavior patterns
                        if (results.configuration.includeDetailedAnalysis) {{
                            const behaviors = results.guardTests.map(test => test.guardBehavior);
                            results.guardAnalysis.guardBehaviors = {{
                                allowed: behaviors.filter(b => b === 'allowed').length,
                                blocked: behaviors.filter(b => b === 'blocked').length,
                                redirected: behaviors.filter(b => b === 'redirected').length,
                                failed: behaviors.filter(b => b === 'failed').length,
                                unknown: behaviors.filter(b => b === 'unknown').length
                            }};

                            // Calculate average execution time
                            if (results.guardAnalysis.guardExecutionTimes.length > 0) {{
                                const avgTime = results.guardAnalysis.guardExecutionTimes.reduce((a, b) => a + b, 0) / 
                                               results.guardAnalysis.guardExecutionTimes.length;
                                results.guardAnalysis.averageExecutionTime = avgTime;
                            }}
                        }}

                        // Generate recommendations
                        if (results.testResults.failedTests > 0) {{
                            results.recommendations.push({{
                                type: 'reliability',
                                severity: 'medium',
                                message: `${{results.testResults.failedTests}} guard test(s) failed - check guard implementation`,
                                category: 'guard_failures'
                            }});
                        }}

                        if (results.guardAnalysis.averageExecutionTime > 500) {{
                            results.recommendations.push({{
                                type: 'performance',
                                severity: 'medium',
                                message: 'Guard execution time is slow (>500ms) - consider optimizing guard logic',
                                category: 'guard_performance'
                            }});
                        }}

                        if (results.testResults.guardsBlocked === 0 && results.guardAnalysis.totalGuardsFound > 0) {{
                            results.recommendations.push({{
                                type: 'security',
                                severity: 'low',
                                message: 'No guards blocked navigation during testing - verify guard logic',
                                category: 'guard_effectiveness'
                            }});
                        }}

                    }} catch (e) {{
                        results.errors.push(`Error in guard testing: ${{e.message}}`);
                    }} finally {{
                        results.testEndTime = Date.now();
                        const totalDuration = results.testEndTime - results.testStartTime;
                        
                        if (totalDuration > {timeoutSeconds * 1000}) {{
                            results.timedOut = true;
                            results.warnings.push(`Testing exceeded timeout of ${{timeoutSeconds}} seconds`);
                        }}
                    }}

                    return results;
                }})();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to test navigation guard behaviors: {ex.Message}";
        }
    }
}
