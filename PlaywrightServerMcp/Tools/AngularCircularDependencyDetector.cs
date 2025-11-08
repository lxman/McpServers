using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Playwright.Core.Services;

namespace PlaywrightServerMcp.Tools;

/// <summary>
/// Angular Circular Dependency Detection Tool
/// 
/// Implements ANG-017: Circular Dependency Detection
/// Provides specialized detection and analysis of circular dependencies in Angular applications
/// with comprehensive resolution suggestions and architectural recommendations.
/// 
/// Key Features:
/// - Advanced circular dependency detection algorithms
/// - Dependency cycle analysis with severity assessment
/// - Comprehensive resolution suggestions and strategies
/// - Architecture impact analysis
/// - Performance impact assessment
/// - Refactoring recommendations
/// 
/// Dependencies: ANG-016 (Service Dependency Graph Analysis)
/// </summary>
[McpServerToolType]
public class AngularCircularDependencyDetector(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Detect circular dependencies in Angular applications with comprehensive analysis
    /// 
    /// ANG-017 Implementation: Circular Dependency Detection
    /// 
    /// This function provides specialized detection and analysis of circular dependencies:
    /// - Advanced dependency cycle detection using multiple algorithms
    /// - Comprehensive cycle analysis with path tracing
    /// - Severity assessment and impact analysis
    /// - Detailed resolution suggestions and refactoring strategies
    /// - Architecture recommendations for preventing future cycles
    /// - Performance impact assessment
    /// 
    /// Features:
    /// - Multiple detection algorithms (DFS, Tarjan's, Component analysis)
    /// - Cycle classification by type and severity
    /// - Detailed path analysis with shortest cycle detection
    /// - Resolution strategy recommendations
    /// - Refactoring suggestions with implementation guidance
    /// - Architecture pattern recommendations
    /// - Performance and maintainability impact analysis
    /// 
    /// Returns comprehensive analysis including:
    /// - Detected circular dependency cycles
    /// - Cycle classification and severity analysis
    /// - Resolution suggestions with implementation steps
    /// - Architecture recommendations
    /// - Performance impact assessment
    /// </summary>
    /// <param name="sessionId">Session ID for browser context</param>
    /// <param name="includeResolutionSuggestions">Include detailed resolution suggestions (default: true)</param>
    /// <param name="analyzeArchitectureImpact">Analyze architecture impact (default: true)</param>
    /// <param name="includePerformanceAnalysis">Include performance impact analysis (default: true)</param>
    /// <param name="detectionMethod">Detection method: 'dfs', 'tarjan', 'comprehensive' (default: 'comprehensive')</param>
    /// <returns>Comprehensive circular dependency analysis results</returns>
    [McpServerTool]
    [Description("Detect circular dependencies in Angular applications with comprehensive analysis and resolution suggestions. See skills/playwright-mcp/tools/angular/circular-dependency-detector.md.")]
    public async Task<string> DetectCircularDependencies(
        string sessionId = "default",
        bool includeResolutionSuggestions = true,
        bool analyzeArchitectureImpact = true,
        bool includePerformanceAnalysis = true,
        string detectionMethod = "comprehensive")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return JsonSerializer.Serialize(new { error = "No active browser session found", sessionId }, JsonOptions);

            // Execute comprehensive circular dependency detection with embedded parameters
            var jsCode = $$"""

                                           (function() {
                                               const includeResolutionSuggestions = {{includeResolutionSuggestions.ToString().ToLower()}};
                                               const analyzeArchitectureImpact = {{analyzeArchitectureImpact.ToString().ToLower()}};
                                               const includePerformanceAnalysis = {{includePerformanceAnalysis.ToString().ToLower()}};
                                               const detectionMethod = '{{detectionMethod}}';
                                               
                                               // Check if Angular is available
                                               const hasAngular = typeof ng !== 'undefined' || 
                                                                typeof window.ng !== 'undefined' || 
                                                                typeof getAllAngularRootElements !== 'undefined' ||
                                                                document.querySelector('[ng-version]') !== null ||
                                                                document.querySelector('[data-ng-version]') !== null ||
                                                                window.Zone !== undefined;

                                               if (!hasAngular) {
                                                   return {
                                                       success: false,
                                                       error: 'Angular not detected on this page',
                                                       recommendation: 'This tool requires an Angular application to analyze circular dependencies'
                                                   };
                                               }

                                               const analysisStartTime = performance.now();
                                               const results = {
                                                   success: true,
                                                   timestamp: new Date().toISOString(),
                                                   analysisMetadata: {
                                                       analysisType: 'circular_dependency_detection',
                                                       detectionMethod: detectionMethod,
                                                       includeResolutionSuggestions: includeResolutionSuggestions,
                                                       analyzeArchitectureImpact: analyzeArchitectureImpact,
                                                       includePerformanceAnalysis: includePerformanceAnalysis,
                                                       analysisStartTime: analysisStartTime
                                                   }
                                               };

                                               // Angular Detection and Environment Analysis
                                               const angularInfo = detectAngularEnvironment();
                                               results.angularInfo = angularInfo;

                                               // Service and Component Discovery
                                               const dependencyDiscovery = discoverDependencies();
                                               results.dependencyDiscovery = dependencyDiscovery;

                                               // Circular Dependency Detection
                                               const circularAnalysis = detectCircularDependenciesComprehensive(
                                                   dependencyDiscovery.graph, 
                                                   detectionMethod
                                               );
                                               results.circularAnalysis = circularAnalysis;

                                               // Cycle Classification and Analysis
                                               const cycleClassification = classifyDependencyCycles(circularAnalysis.cycles);
                                               results.cycleClassification = cycleClassification;

                                               // Resolution Suggestions (if enabled)
                                               if (includeResolutionSuggestions) {
                                                   const resolutionSuggestions = generateResolutionSuggestions(
                                                       circularAnalysis.cycles,
                                                       dependencyDiscovery.graph
                                                   );
                                                   results.resolutionSuggestions = resolutionSuggestions;
                                               }

                                               // Architecture Impact Analysis (if enabled)
                                               if (analyzeArchitectureImpact) {
                                                   const architectureImpact = analyzeArchitecturalImpact(
                                                       circularAnalysis.cycles,
                                                       dependencyDiscovery.graph
                                                   );
                                                   results.architectureImpact = architectureImpact;
                                               }

                                               // Performance Impact Analysis (if enabled)
                                               if (includePerformanceAnalysis) {
                                                   const performanceAnalysis = analyzePerformanceImpact(
                                                       circularAnalysis.cycles,
                                                       dependencyDiscovery.graph
                                                   );
                                                   results.performanceAnalysis = performanceAnalysis;
                                               }

                                               // Prevention Recommendations
                                               const preventionRecommendations = generatePreventionRecommendations(
                                                   circularAnalysis,
                                                   cycleClassification
                                               );
                                               results.preventionRecommendations = preventionRecommendations;

                                               // Analysis Summary
                                               const analysisEndTime = performance.now();
                                               results.summary = {
                                                   executionTime: Math.round(analysisEndTime - analysisStartTime),
                                                   totalCycles: circularAnalysis.cycles.length,
                                                   criticalCycles: cycleClassification.critical.length,
                                                   majorCycles: cycleClassification.major.length,
                                                   minorCycles: cycleClassification.minor.length,
                                                   overallSeverity: circularAnalysis.overallSeverity,
                                                   affectedComponents: circularAnalysis.affectedComponents.length,
                                                   resolutionComplexity: calculateResolutionComplexity(circularAnalysis.cycles),
                                                   analysisComplete: true
                                               };

                                               return results;

                                               // Helper Functions

                                               function detectAngularEnvironment() {
                                                   const info = {
                                                       detected: true,
                                                       version: 'unknown',
                                                       devMode: false,
                                                       zoneless: false,
                                                       devToolsAvailable: false,
                                                       dependencyIntrospectionAvailable: false
                                                   };

                                                   // Version detection
                                                   const versionElement = document.querySelector('[ng-version]') || 
                                                                        document.querySelector('[data-ng-version]');
                                                   if (versionElement) {
                                                       info.version = versionElement.getAttribute('ng-version') || 
                                                                    versionElement.getAttribute('data-ng-version') || 'unknown';
                                                   }

                                                   // Dev mode detection
                                                   info.devMode = typeof ng !== 'undefined' && 
                                                                 ng.probe !== undefined;

                                                   // Zoneless detection
                                                   info.zoneless = typeof Zone === 'undefined' || 
                                                                 (typeof ng !== 'undefined' && ng.getOwningComponent !== undefined);

                                                   // DevTools availability
                                                   info.devToolsAvailable = typeof ng !== 'undefined' && 
                                                                          ng.getInjector !== undefined;

                                                   // Dependency introspection capability
                                                   info.dependencyIntrospectionAvailable = info.devToolsAvailable || 
                                                                                         (typeof window.ngDevMode !== 'undefined');

                                                   return info;
                                               }

                                               function discoverDependencies() {
                                                   const discovery = {
                                                       graph: {
                                                           nodes: [],
                                                           edges: []
                                                       },
                                                       services: [],
                                                       components: [],
                                                       modules: [],
                                                       discoveryMethods: []
                                                   };

                                                   try {
                                                       // Method 1: Angular DevTools API
                                                       if (typeof ng !== 'undefined' && ng.getInjector) {
                                                           discovery.discoveryMethods.push('devtools_api');
                                                           const devToolsData = discoverViaDevTools();
                                                           mergeDiscoveryData(discovery, devToolsData);
                                                       }

                                                       // Method 2: DOM and Component Analysis
                                                       discovery.discoveryMethods.push('dom_component_analysis');
                                                       const domData = discoverViaDOMAnalysis();
                                                       mergeDiscoveryData(discovery, domData);

                                                       // Method 3: Script Analysis
                                                       discovery.discoveryMethods.push('script_analysis');
                                                       const scriptData = discoverViaScriptAnalysis();
                                                       mergeDiscoveryData(discovery, scriptData);

                                                       // Build dependency graph
                                                       buildDependencyGraph(discovery);

                                                   } catch (error) {
                                                       discovery.error = error.message;
                                                   }

                                                   return discovery;
                                               }

                                               function discoverViaDevTools() {
                                                   const data = {
                                                       services: [],
                                                       components: [],
                                                       modules: []
                                                   };

                                                   try {
                                                       // Get all Angular elements
                                                       const angularElements = document.querySelectorAll('[ng-version], [data-ng-version]');
                                                       
                                                       for (const element of angularElements) {
                                                           try {
                                                               const injector = ng.getInjector(element);
                                                               if (injector) {
                                                                   // Get component information
                                                                   const component = ng.getComponent(element);
                                                                   if (component) {
                                                                       data.components.push({
                                                                           name: component.constructor.name,
                                                                           element: element.tagName.toLowerCase(),
                                                                           dependencies: extractComponentDependencies(component),
                                                                           injector: 'available'
                                                                       });
                                                                   }

                                                                   // Get common services
                                                                   const commonServices = [
                                                                       'Router', 'HttpClient', 'ActivatedRoute', 'Location',
                                                                       'FormBuilder', 'Renderer2', 'ElementRef', 'ChangeDetectorRef'
                                                                   ];

                                                                   for (const serviceName of commonServices) {
                                                                       try {
                                                                           const service = injector.get(serviceName, null);
                                                                           if (service) {
                                                                               data.services.push({
                                                                                   name: serviceName,
                                                                                   type: 'angular_built_in',
                                                                                   dependencies: extractServiceDependencies(service)
                                                                               });
                                                                           }
                                                                       } catch (e) {
                                                                           // Service not available
                                                                       }
                                                                   }
                                                               }
                                                           } catch (e) {
                                                               // Element doesn't have injector
                                                           }
                                                       }
                                                   } catch (error) {
                                                       data.error = error.message;
                                                   }

                                                   return data;
                                               }

                                               function discoverViaDOMAnalysis() {
                                                   const data = {
                                                       services: [],
                                                       components: [],
                                                       modules: []
                                                   };

                                                   try {
                                                       // Find Angular components by element selectors
                                                       const componentElements = document.querySelectorAll('[ng-reflect-router-outlet], app-*, *[data-cy]');
                                                       
                                                       for (const element of componentElements) {
                                                           const componentName = element.tagName.toLowerCase();
                                                           if (componentName.includes('-') || componentName.startsWith('app')) {
                                                               data.components.push({
                                                                   name: componentName,
                                                                   element: componentName,
                                                                   dependencies: [],
                                                                   discoveryMethod: 'dom_analysis'
                                                               });
                                                           }
                                                       }

                                                       // Look for router-outlet and other Angular-specific elements
                                                       const routerOutlets = document.querySelectorAll('router-outlet');
                                                       const angularForms = document.querySelectorAll('[formGroup], [ngModel]');
                                                       
                                                       if (routerOutlets.length > 0) {
                                                           data.services.push({
                                                               name: 'Router',
                                                               type: 'angular_routing',
                                                               dependencies: ['Location', 'ActivatedRoute']
                                                           });
                                                       }

                                                       if (angularForms.length > 0) {
                                                           data.services.push({
                                                               name: 'FormBuilder',
                                                               type: 'angular_forms',
                                                               dependencies: []
                                                           });
                                                       }

                                                   } catch (error) {
                                                       data.error = error.message;
                                                   }

                                                   return data;
                                               }

                                               function discoverViaScriptAnalysis() {
                                                   const data = {
                                                       services: [],
                                                       components: [],
                                                       modules: []
                                                   };

                                                   try {
                                                       const scripts = document.querySelectorAll('script');
                                                       
                                                       for (const script of scripts) {
                                                           const content = script.textContent || script.innerText || '';
                                                           
                                                           // Look for Angular patterns
                                                           const injectionPatterns = [
                                                               /@Injectable\\s*\\(\\s*\\{[^}]*\\}\\s*\\)/g,
                                                               /@Component\\s*\\(\\s*\\{[^}]*\\}\\s*\\)/g,
                                                               /@NgModule\\s*\\(\\s*\\{[^}]*\\}\\s*\\)/g
                                                           ];

                                                           for (const pattern of injectionPatterns) {
                                                               const matches = content.match(pattern);
                                                               if (matches) {
                                                                   for (const match of matches) {
                                                                       const type = match.includes('@Injectable') ? 'service' :
                                                                                  match.includes('@Component') ? 'component' : 'module';
                                                                       
                                                                       if (type === 'service') {
                                                                           data.services.push({
                                                                               name: `CustomService_${data.services.length}`,
                                                                               type: 'custom_service',
                                                                               dependencies: extractDependenciesFromDeclaration(match)
                                                                           });
                                                                       } else if (type === 'component') {
                                                                           data.components.push({
                                                                               name: `CustomComponent_${data.components.length}`,
                                                                               dependencies: extractDependenciesFromDeclaration(match)
                                                                           });
                                                                       }
                                                                   }
                                                               }
                                                           }

                                                           // Look for constructor injection patterns
                                                           const constructorMatches = content.match(/constructor\\s*\\([^)]+\\)/g);
                                                           if (constructorMatches) {
                                                               for (const constructorMatch of constructorMatches) {
                                                                   const dependencies = extractConstructorDependencies(constructorMatch);
                                                                   if (dependencies.length > 0) {
                                                                       data.services.push({
                                                                           name: `ServiceWithDeps_${data.services.length}`,
                                                                           type: 'unknown',
                                                                           dependencies: dependencies
                                                                       });
                                                                   }
                                                               }
                                                           }
                                                       }

                                                   } catch (error) {
                                                       data.error = error.message;
                                                   }

                                                   return data;
                                               }

                                               function extractComponentDependencies(component) {
                                                   const dependencies = [];
                                                   
                                                   try {
                                                       if (component && component.constructor) {
                                                           const constructorString = component.constructor.toString();
                                                           const paramMatches = constructorString.match(/function[^(]*\\(([^)]*)\\)/);
                                                           
                                                           if (paramMatches && paramMatches[1]) {
                                                               const params = paramMatches[1].split(',');
                                                               for (const param of params) {
                                                                   if (param.trim()) {
                                                                       dependencies.push({
                                                                           name: param.trim(),
                                                                           type: 'constructor_injection'
                                                                       });
                                                                   }
                                                               }
                                                           }
                                                       }
                                                   } catch (error) {
                                                       // Dependency extraction failed
                                                   }

                                                   return dependencies;
                                               }

                                               function extractServiceDependencies(service) {
                                                   const dependencies = [];
                                                   
                                                   try {
                                                       if (service && service.constructor) {
                                                           const constructorString = service.constructor.toString();
                                                           const paramMatches = constructorString.match(/function[^(]*\\(([^)]*)\\)/);
                                                           
                                                           if (paramMatches && paramMatches[1]) {
                                                               const params = paramMatches[1].split(',');
                                                               for (const param of params) {
                                                                   if (param.trim()) {
                                                                       dependencies.push({
                                                                           name: param.trim(),
                                                                           type: 'constructor_injection'
                                                                       });
                                                                   }
                                                               }
                                                           }
                                                       }
                                                   } catch (error) {
                                                       // Dependency extraction failed
                                                   }

                                                   return dependencies;
                                               }

                                               function extractDependenciesFromDeclaration(declaration) {
                                                   const dependencies = [];
                                                   
                                                   try {
                                                       // Look for constructor dependencies in declaration
                                                       const constructorMatch = declaration.match(/constructor\\s*\\(([^)]+)\\)/);
                                                       if (constructorMatch) {
                                                           const params = constructorMatch[1].split(',');
                                                           for (const param of params) {
                                                               const paramParts = param.split(':');
                                                               if (paramParts.length > 1) {
                                                                   dependencies.push({
                                                                       name: paramParts[1].trim(),
                                                                       type: 'constructor_injection'
                                                                   });
                                                               }
                                                           }
                                                       }
                                                   } catch (error) {
                                                       // Extraction failed
                                                   }

                                                   return dependencies;
                                               }

                                               function extractConstructorDependencies(constructorString) {
                                                   const dependencies = [];
                                                   
                                                   try {
                                                       const paramMatch = constructorString.match(/constructor\\s*\\(([^)]+)\\)/);
                                                       if (paramMatch) {
                                                           const params = paramMatch[1].split(',');
                                                           for (const param of params) {
                                                               const trimmed = param.trim();
                                                               if (trimmed && trimmed.includes(':')) {
                                                                   const parts = trimmed.split(':');
                                                                   if (parts.length > 1) {
                                                                       dependencies.push({
                                                                           name: parts[1].trim(),
                                                                           type: 'constructor_injection'
                                                                       });
                                                                   }
                                                               }
                                                           }
                                                       }
                                                   } catch (error) {
                                                       // Extraction failed
                                                   }

                                                   return dependencies;
                                               }

                                               function mergeDiscoveryData(discovery, newData) {
                                                   try {
                                                       if (newData.services) {
                                                           discovery.services.push(...newData.services);
                                                       }
                                                       if (newData.components) {
                                                           discovery.components.push(...newData.components);
                                                       }
                                                       if (newData.modules) {
                                                           discovery.modules.push(...newData.modules);
                                                       }
                                                   } catch (error) {
                                                       // Merge failed
                                                   }
                                               }

                                               function buildDependencyGraph(discovery) {
                                                   try {
                                                       const allEntities = [
                                                           ...discovery.services.map(s => ({...s, entityType: 'service'})),
                                                           ...discovery.components.map(c => ({...c, entityType: 'component'})),
                                                           ...discovery.modules.map(m => ({...m, entityType: 'module'}))
                                                       ];

                                                       // Create nodes
                                                       for (const entity of allEntities) {
                                                           discovery.graph.nodes.push({
                                                               id: entity.name,
                                                               name: entity.name,
                                                               type: entity.entityType,
                                                               subType: entity.type || 'unknown',
                                                               dependencyCount: entity.dependencies ? entity.dependencies.length : 0
                                                           });
                                                       }

                                                       // Create edges
                                                       for (const entity of allEntities) {
                                                           if (entity.dependencies) {
                                                               for (const dependency of entity.dependencies) {
                                                                   const dependencyName = typeof dependency === 'string' ? dependency : dependency.name;
                                                                   
                                                                   discovery.graph.edges.push({
                                                                       source: entity.name,
                                                                       target: dependencyName,
                                                                       type: typeof dependency === 'object' ? dependency.type : 'unknown',
                                                                       weight: 1
                                                                   });
                                                               }
                                                           }
                                                       }

                                                       // Remove duplicate nodes and edges
                                                       discovery.graph.nodes = removeDuplicateNodes(discovery.graph.nodes);
                                                       discovery.graph.edges = removeDuplicateEdges(discovery.graph.edges);

                                                   } catch (error) {
                                                       discovery.graph.error = error.message;
                                                   }
                                               }

                                               function removeDuplicateNodes(nodes) {
                                                   const seen = new Set();
                                                   return nodes.filter(node => {
                                                       if (seen.has(node.id)) {
                                                           return false;
                                                       }
                                                       seen.add(node.id);
                                                       return true;
                                                   });
                                               }

                                               function removeDuplicateEdges(edges) {
                                                   const seen = new Set();
                                                   return edges.filter(edge => {
                                                       const key = `${edge.source}->${edge.target}`;
                                                       if (seen.has(key)) {
                                                           return false;
                                                       }
                                                       seen.add(key);
                                                       return true;
                                                   });
                                               }

                                               function detectCircularDependenciesComprehensive(graph, method) {
                                                   const analysis = {
                                                       cycles: [],
                                                       cycleCount: 0,
                                                       affectedComponents: [],
                                                       overallSeverity: 'none',
                                                       detectionMethods: [],
                                                       analysisMetrics: {}
                                                   };

                                                   try {
                                                       let detectedCycles = [];

                                                       // Choose detection method(s)
                                                       if (method === 'dfs' || method === 'comprehensive') {
                                                           analysis.detectionMethods.push('depth_first_search');
                                                           const dfsCycles = detectCyclesDFS(graph);
                                                           detectedCycles.push(...dfsCycles);
                                                       }

                                                       if (method === 'tarjan' || method === 'comprehensive') {
                                                           analysis.detectionMethods.push('tarjan_algorithm');
                                                           const tarjanCycles = detectCyclesTarjan(graph);
                                                           detectedCycles.push(...tarjanCycles);
                                                       }

                                                       if (method === 'comprehensive') {
                                                           analysis.detectionMethods.push('component_analysis');
                                                           const componentCycles = detectCyclesComponentAnalysis(graph);
                                                           detectedCycles.push(...componentCycles);
                                                       }

                                                       // Remove duplicate cycles
                                                       analysis.cycles = removeDuplicateCycles(detectedCycles);
                                                       analysis.cycleCount = analysis.cycles.length;

                                                       // Extract affected components
                                                       const affectedSet = new Set();
                                                       for (const cycle of analysis.cycles) {
                                                           for (const component of cycle.path) {
                                                               affectedSet.add(component);
                                                           }
                                                       }
                                                       analysis.affectedComponents = Array.from(affectedSet);

                                                       // Assess overall severity
                                                       analysis.overallSeverity = assessOverallSeverity(analysis.cycles);

                                                       // Calculate analysis metrics
                                                       analysis.analysisMetrics = {
                                                           totalNodes: graph.nodes.length,
                                                           totalEdges: graph.edges.length,
                                                           cycleComplexity: calculateCycleComplexity(analysis.cycles),
                                                           longestCycle: Math.max(...analysis.cycles.map(c => c.path.length), 0),
                                                           shortestCycle: analysis.cycles.length > 0 ? Math.min(...analysis.cycles.map(c => c.path.length)) : 0
                                                       };

                                                   } catch (error) {
                                                       analysis.error = error.message;
                                                   }

                                                   return analysis;
                                               }

                                               function detectCyclesDFS(graph) {
                                                   const cycles = [];
                                                   const visited = new Set();
                                                   const recursionStack = new Set();

                                                   for (const node of graph.nodes) {
                                                       if (!visited.has(node.id)) {
                                                           const foundCycles = dfsVisit(node.id, graph, visited, recursionStack, []);
                                                           cycles.push(...foundCycles);
                                                       }
                                                   }

                                                   return cycles;
                                               }

                                               function dfsVisit(nodeId, graph, visited, recursionStack, path) {
                                                   const cycles = [];
                                                   visited.add(nodeId);
                                                   recursionStack.add(nodeId);
                                                   path.push(nodeId);

                                                   const edges = graph.edges.filter(edge => edge.source === nodeId);
                                                   
                                                   for (const edge of edges) {
                                                       const target = edge.target;
                                                       
                                                       if (!visited.has(target)) {
                                                           const foundCycles = dfsVisit(target, graph, visited, recursionStack, [...path]);
                                                           cycles.push(...foundCycles);
                                                       } else if (recursionStack.has(target)) {
                                                           // Found a cycle
                                                           const cycleStart = path.indexOf(target);
                                                           const cyclePath = path.slice(cycleStart);
                                                           cyclePath.push(target); // Complete the cycle
                                                           
                                                           cycles.push({
                                                               path: cyclePath,
                                                               length: cyclePath.length - 1,
                                                               detectionMethod: 'dfs',
                                                               severity: assessCycleSeverity(cyclePath),
                                                               type: classifyCycleType(cyclePath, graph)
                                                           });
                                                       }
                                                   }

                                                   recursionStack.delete(nodeId);
                                                   return cycles;
                                               }

                                               function detectCyclesTarjan(graph) {
                                                   const cycles = [];
                                                   const index = new Map();
                                                   const lowLink = new Map();
                                                   const onStack = new Set();
                                                   const stack = [];
                                                   let indexCounter = 0;

                                                   for (const node of graph.nodes) {
                                                       if (!index.has(node.id)) {
                                                           tarjanStrongConnect(node.id, graph, index, lowLink, onStack, stack, indexCounter, cycles);
                                                       }
                                                   }

                                                   return cycles;
                                               }

                                               function tarjanStrongConnect(nodeId, graph, index, lowLink, onStack, stack, indexCounter, cycles) {
                                                   index.set(nodeId, indexCounter);
                                                   lowLink.set(nodeId, indexCounter);
                                                   indexCounter++;
                                                   stack.push(nodeId);
                                                   onStack.add(nodeId);

                                                   const edges = graph.edges.filter(edge => edge.source === nodeId);
                                                   
                                                   for (const edge of edges) {
                                                       const target = edge.target;
                                                       
                                                       if (!index.has(target)) {
                                                           tarjanStrongConnect(target, graph, index, lowLink, onStack, stack, indexCounter, cycles);
                                                           lowLink.set(nodeId, Math.min(lowLink.get(nodeId), lowLink.get(target)));
                                                       } else if (onStack.has(target)) {
                                                           lowLink.set(nodeId, Math.min(lowLink.get(nodeId), index.get(target)));
                                                       }
                                                   }

                                                   // If nodeId is a root node, pop the stack and create strongly connected component
                                                   if (lowLink.get(nodeId) === index.get(nodeId)) {
                                                       const component = [];
                                                       let w;
                                                       do {
                                                           w = stack.pop();
                                                           onStack.delete(w);
                                                           component.push(w);
                                                       } while (w !== nodeId);

                                                       // If component has more than one node or self-loop, it's a cycle
                                                       if (component.length > 1 || hasSelfLoop(nodeId, graph)) {
                                                           cycles.push({
                                                               path: component.reverse(),
                                                               length: component.length,
                                                               detectionMethod: 'tarjan',
                                                               severity: assessCycleSeverity(component),
                                                               type: classifyCycleType(component, graph)
                                                           });
                                                       }
                                                   }
                                               }

                                               function detectCyclesComponentAnalysis(graph) {
                                                   const cycles = [];
                                                   
                                                   try {
                                                       // Look for specific Angular component dependency patterns
                                                       const componentNodes = graph.nodes.filter(node => node.type === 'component');
                                                       
                                                       for (const component of componentNodes) {
                                                           const componentCycles = analyzeComponentDependencies(component, graph);
                                                           cycles.push(...componentCycles);
                                                       }

                                                       // Look for service injection cycles
                                                       const serviceNodes = graph.nodes.filter(node => node.type === 'service');
                                                       
                                                       for (const service of serviceNodes) {
                                                           const serviceCycles = analyzeServiceDependencies(service, graph);
                                                           cycles.push(...serviceCycles);
                                                       }

                                                   } catch (error) {
                                                       // Component analysis failed
                                                   }

                                                   return cycles;
                                               }

                                               function analyzeComponentDependencies(component, graph) {
                                                   const cycles = [];
                                                   const visited = new Set();
                                                   
                                                   try {
                                                       const path = [component.id];
                                                       const foundCycles = traceComponentPath(component.id, graph, visited, path);
                                                       
                                                       for (const cycle of foundCycles) {
                                                           cycles.push({
                                                               ...cycle,
                                                               detectionMethod: 'component_analysis',
                                                               componentSpecific: true
                                                           });
                                                       }
                                                   } catch (error) {
                                                       // Component dependency analysis failed
                                                   }

                                                   return cycles;
                                               }

                                               function analyzeServiceDependencies(service, graph) {
                                                   const cycles = [];
                                                   const visited = new Set();
                                                   
                                                   try {
                                                       const path = [service.id];
                                                       const foundCycles = traceServicePath(service.id, graph, visited, path);
                                                       
                                                       for (const cycle of foundCycles) {
                                                           cycles.push({
                                                               ...cycle,
                                                               detectionMethod: 'service_analysis',
                                                               serviceSpecific: true
                                                           });
                                                       }
                                                   } catch (error) {
                                                       // Service dependency analysis failed
                                                   }

                                                   return cycles;
                                               }

                                               function traceComponentPath(componentId, graph, visited, path) {
                                                   const cycles = [];
                                                   
                                                   if (visited.has(componentId)) {
                                                       const cycleStart = path.indexOf(componentId);
                                                       if (cycleStart !== -1) {
                                                           const cyclePath = path.slice(cycleStart);
                                                           cyclePath.push(componentId);
                                                           
                                                           cycles.push({
                                                               path: cyclePath,
                                                               length: cyclePath.length - 1,
                                                               severity: assessCycleSeverity(cyclePath),
                                                               type: 'component_cycle'
                                                           });
                                                       }
                                                       return cycles;
                                                   }

                                                   visited.add(componentId);
                                                   const edges = graph.edges.filter(edge => edge.source === componentId);
                                                   
                                                   for (const edge of edges) {
                                                       const target = edge.target;
                                                       const targetNode = graph.nodes.find(n => n.id === target);
                                                       
                                                       if (targetNode && (targetNode.type === 'component' || targetNode.type === 'service')) {
                                                           const newPath = [...path, target];
                                                           const foundCycles = traceComponentPath(target, graph, new Set(visited), newPath);
                                                           cycles.push(...foundCycles);
                                                       }
                                                   }

                                                   return cycles;
                                               }

                                               function traceServicePath(serviceId, graph, visited, path) {
                                                   const cycles = [];
                                                   
                                                   if (visited.has(serviceId)) {
                                                       const cycleStart = path.indexOf(serviceId);
                                                       if (cycleStart !== -1) {
                                                           const cyclePath = path.slice(cycleStart);
                                                           cyclePath.push(serviceId);
                                                           
                                                           cycles.push({
                                                               path: cyclePath,
                                                               length: cyclePath.length - 1,
                                                               severity: assessCycleSeverity(cyclePath),
                                                               type: 'service_cycle'
                                                           });
                                                       }
                                                       return cycles;
                                                   }

                                                   visited.add(serviceId);
                                                   const edges = graph.edges.filter(edge => edge.source === serviceId);
                                                   
                                                   for (const edge of edges) {
                                                       const target = edge.target;
                                                       const targetNode = graph.nodes.find(n => n.id === target);
                                                       
                                                       if (targetNode && targetNode.type === 'service') {
                                                           const newPath = [...path, target];
                                                           const foundCycles = traceServicePath(target, graph, new Set(visited), newPath);
                                                           cycles.push(...foundCycles);
                                                       }
                                                   }

                                                   return cycles;
                                               }

                                               function hasSelfLoop(nodeId, graph) {
                                                   return graph.edges.some(edge => edge.source === nodeId && edge.target === nodeId);
                                               }

                                               function removeDuplicateCycles(cycles) {
                                                   const seen = new Set();
                                                   return cycles.filter(cycle => {
                                                       const normalized = normalizeCyclePath(cycle.path);
                                                       const key = normalized.join('-');
                                                       if (seen.has(key)) {
                                                           return false;
                                                       }
                                                       seen.add(key);
                                                       return true;
                                                   });
                                               }

                                               function normalizeCyclePath(path) {
                                                   // Normalize cycle path to start with the lexicographically smallest element
                                                   const minIndex = path.indexOf(Math.min(...path.map(p => p.charCodeAt ? p.charCodeAt(0) : 0)));
                                                   return [...path.slice(minIndex), ...path.slice(0, minIndex)];
                                               }

                                               function assessCycleSeverity(cyclePath) {
                                                   const length = cyclePath.length - 1; // Subtract 1 for the repeated element
                                                   
                                                   if (length <= 2) {
                                                       return 'minor';
                                                   } else if (length <= 4) {
                                                       return 'major';
                                                   } else {
                                                       return 'critical';
                                                   }
                                               }

                                               function classifyCycleType(cyclePath, graph) {
                                                   const nodeTypes = cyclePath.map(nodeId => {
                                                       const node = graph.nodes.find(n => n.id === nodeId);
                                                       return node ? node.type : 'unknown';
                                                   });

                                                   const hasComponent = nodeTypes.includes('component');
                                                   const hasService = nodeTypes.includes('service');
                                                   const hasModule = nodeTypes.includes('module');

                                                   if (hasComponent && hasService && hasModule) {
                                                       return 'mixed_architecture_cycle';
                                                   } else if (hasComponent && hasService) {
                                                       return 'component_service_cycle';
                                                   } else if (hasService) {
                                                       return 'service_cycle';
                                                   } else if (hasComponent) {
                                                       return 'component_cycle';
                                                   } else {
                                                       return 'unknown_cycle';
                                                   }
                                               }

                                               function assessOverallSeverity(cycles) {
                                                   if (cycles.length === 0) {
                                                       return 'none';
                                                   }

                                                   const criticalCount = cycles.filter(c => c.severity === 'critical').length;
                                                   const majorCount = cycles.filter(c => c.severity === 'major').length;
                                                   const minorCount = cycles.filter(c => c.severity === 'minor').length;

                                                   if (criticalCount > 0) {
                                                       return 'critical';
                                                   } else if (majorCount > 2) {
                                                       return 'major';
                                                   } else if (majorCount > 0 || minorCount > 3) {
                                                       return 'moderate';
                                                   } else {
                                                       return 'minor';
                                                   }
                                               }

                                               function calculateCycleComplexity(cycles) {
                                                   if (cycles.length === 0) return 0;
                                                   
                                                   const totalComplexity = cycles.reduce((sum, cycle) => {
                                                       return sum + (cycle.length * cycle.length); // Quadratic complexity
                                                   }, 0);
                                                   
                                                   return totalComplexity / cycles.length;
                                               }

                                               function classifyDependencyCycles(cycles) {
                                                   const classification = {
                                                       critical: [],
                                                       major: [],
                                                       minor: [],
                                                       byType: {
                                                           component_cycle: [],
                                                           service_cycle: [],
                                                           mixed_architecture_cycle: [],
                                                           component_service_cycle: [],
                                                           unknown_cycle: []
                                                       },
                                                       analysis: {
                                                           mostComplexCycle: null,
                                                           longestCycle: null,
                                                           shortestCycle: null,
                                                           averageLength: 0
                                                       }
                                                   };

                                                   try {
                                                       // Classify by severity
                                                       for (const cycle of cycles) {
                                                           if (cycle.severity === 'critical') {
                                                               classification.critical.push(cycle);
                                                           } else if (cycle.severity === 'major') {
                                                               classification.major.push(cycle);
                                                           } else {
                                                               classification.minor.push(cycle);
                                                           }

                                                           // Classify by type
                                                           const type = cycle.type || 'unknown_cycle';
                                                           if (classification.byType[type]) {
                                                               classification.byType[type].push(cycle);
                                                           }
                                                       }

                                                       // Analysis
                                                       if (cycles.length > 0) {
                                                           const lengths = cycles.map(c => c.length);
                                                           classification.analysis.averageLength = lengths.reduce((a, b) => a + b, 0) / lengths.length;
                                                           
                                                           classification.analysis.longestCycle = cycles.reduce((longest, current) => 
                                                               current.length > longest.length ? current : longest);
                                                           
                                                           classification.analysis.shortestCycle = cycles.reduce((shortest, current) => 
                                                               current.length < shortest.length ? current : shortest);
                                                           
                                                           // Most complex is determined by length and severity
                                                           classification.analysis.mostComplexCycle = cycles.reduce((mostComplex, current) => {
                                                               const currentScore = current.length * (current.severity === 'critical' ? 3 : 
                                                                                                   current.severity === 'major' ? 2 : 1);
                                                               const mostComplexScore = mostComplex.length * (mostComplex.severity === 'critical' ? 3 : 
                                                                                                            mostComplex.severity === 'major' ? 2 : 1);
                                                               return currentScore > mostComplexScore ? current : mostComplex;
                                                           });
                                                       }

                                                   } catch (error) {
                                                       classification.error = error.message;
                                                   }

                                                   return classification;
                                               }

                                               function generateResolutionSuggestions(cycles, graph) {
                                                   const suggestions = {
                                                       strategies: [],
                                                       specificSolutions: [],
                                                       refactoringRecommendations: [],
                                                       implementationSteps: []
                                                   };

                                                   try {
                                                       for (const cycle of cycles) {
                                                           const cycleStrategies = generateCycleResolutionStrategies(cycle, graph);
                                                           suggestions.strategies.push(...cycleStrategies);

                                                           const specificSolutions = generateSpecificSolutions(cycle, graph);
                                                           suggestions.specificSolutions.push(...specificSolutions);

                                                           const refactoringRecs = generateRefactoringRecommendations(cycle, graph);
                                                           suggestions.refactoringRecommendations.push(...refactoringRecs);
                                                       }

                                                       // Generate implementation steps
                                                       suggestions.implementationSteps = generateImplementationSteps(cycles, suggestions);

                                                       // Remove duplicates
                                                       suggestions.strategies = removeDuplicateStrategies(suggestions.strategies);
                                                       suggestions.specificSolutions = removeDuplicateSolutions(suggestions.specificSolutions);

                                                   } catch (error) {
                                                       suggestions.error = error.message;
                                                   }

                                                   return suggestions;
                                               }

                                               function generateCycleResolutionStrategies(cycle, graph) {
                                                   const strategies = [];

                                                   try {
                                                       // Strategy 1: Dependency Inversion
                                                       strategies.push({
                                                           name: 'dependency_inversion',
                                                           description: 'Introduce interfaces to break direct dependencies',
                                                           applicability: cycle.type === 'service_cycle' ? 'high' : 'medium',
                                                           complexity: 'medium',
                                                           impact: 'high',
                                                           steps: [
                                                               'Identify the dependency that creates the cycle',
                                                               'Create an interface for the dependency',
                                                               'Implement the interface in the dependent service',
                                                               'Inject the interface instead of the concrete service'
                                                           ]
                                                       });

                                                       // Strategy 2: Event-Driven Pattern
                                                       strategies.push({
                                                           name: 'event_driven_pattern',
                                                           description: 'Use events/observables to decouple components',
                                                           applicability: cycle.type === 'component_cycle' ? 'high' : 'medium',
                                                           complexity: 'medium',
                                                           impact: 'high',
                                                           steps: [
                                                               'Identify communication points in the cycle',
                                                               'Replace direct dependencies with event emission',
                                                               'Implement event listeners in dependent components',
                                                               'Test event flow and error handling'
                                                           ]
                                                       });

                                                       // Strategy 3: Service Extraction
                                                       if (cycle.length > 3) {
                                                           strategies.push({
                                                               name: 'service_extraction',
                                                               description: 'Extract common functionality into a shared service',
                                                               applicability: 'high',
                                                               complexity: 'low',
                                                               impact: 'medium',
                                                               steps: [
                                                                   'Identify common functionality in cycle',
                                                                   'Create a new shared service',
                                                                   'Move common code to the shared service',
                                                                   'Update all cycle participants to use shared service'
                                                               ]
                                                           });
                                                       }

                                                       // Strategy 4: Mediator Pattern
                                                       if (cycle.type === 'mixed_architecture_cycle') {
                                                           strategies.push({
                                                               name: 'mediator_pattern',
                                                               description: 'Introduce a mediator to coordinate interactions',
                                                               applicability: 'high',
                                                               complexity: 'high',
                                                               impact: 'high',
                                                               steps: [
                                                                   'Design mediator interface',
                                                                   'Implement mediator service',
                                                                   'Update cycle participants to use mediator',
                                                                   'Refactor direct dependencies'
                                                               ]
                                                           });
                                                       }

                                                   } catch (error) {
                                                       strategies.push({
                                                           name: 'error_in_strategy_generation',
                                                           error: error.message
                                                       });
                                                   }

                                                   return strategies;
                                               }

                                               function generateSpecificSolutions(cycle, graph) {
                                                   const solutions = [];

                                                   try {
                                                       for (let i = 0; i < cycle.path.length - 1; i++) {
                                                           const source = cycle.path[i];
                                                           const target = cycle.path[i + 1];
                                                           
                                                           const sourceNode = graph.nodes.find(n => n.id === source);
                                                           const targetNode = graph.nodes.find(n => n.id === target);
                                                           
                                                           if (sourceNode && targetNode) {
                                                               solutions.push({
                                                                   cycleId: cycle.path.join('->'),
                                                                   source: source,
                                                                   target: target,
                                                                   relationship: 'dependency',
                                                                   solution: generateDependencyBreakingSolution(sourceNode, targetNode),
                                                                   priority: cycle.severity === 'critical' ? 'high' : 
                                                                           cycle.severity === 'major' ? 'medium' : 'low'
                                                               });
                                                           }
                                                       }

                                                   } catch (error) {
                                                       solutions.push({
                                                           error: error.message,
                                                           cycleId: cycle.path.join('->')
                                                       });
                                                   }

                                                   return solutions;
                                               }

                                               function generateDependencyBreakingSolution(sourceNode, targetNode) {
                                                   const solution = {
                                                       method: 'unknown',
                                                       description: '',
                                                       implementation: [],
                                                       considerations: []
                                                   };

                                                   try {
                                                       if (sourceNode.type === 'service' && targetNode.type === 'service') {
                                                           solution.method = 'service_interface_abstraction';
                                                           solution.description = `Create an interface for ${targetNode.name} and inject it into ${sourceNode.name}`;
                                                           solution.implementation = [
                                                               `Create I${targetNode.name} interface`,
                                                               `Implement interface in ${targetNode.name}`,
                                                               `Update ${sourceNode.name} to depend on I${targetNode.name}`,
                                                               `Configure dependency injection for interface`
                                                           ];
                                                           solution.considerations = [
                                                               'Ensure interface is cohesive and focused',
                                                               'Consider using factory pattern for complex dependencies',
                                                               'Test dependency injection configuration'
                                                           ];
                                                       } else if (sourceNode.type === 'component' && targetNode.type === 'component') {
                                                           solution.method = 'component_communication_pattern';
                                                           solution.description = `Use @Input/@Output or service communication instead of direct dependency`;
                                                           solution.implementation = [
                                                               `Replace direct dependency with @Input/@Output`,
                                                               `Or use shared service for communication`,
                                                               `Implement event-driven communication`,
                                                               `Update component interaction patterns`
                                                           ];
                                                           solution.considerations = [
                                                               'Consider component hierarchy and data flow',
                                                               'Use appropriate communication pattern (parent-child vs siblings)',
                                                               'Ensure proper change detection'
                                                           ];
                                                       } else {
                                                           solution.method = 'mixed_dependency_resolution';
                                                           solution.description = `Break dependency between ${sourceNode.type} and ${targetNode.type}`;
                                                           solution.implementation = [
                                                               'Analyze dependency nature',
                                                               'Choose appropriate decoupling pattern',
                                                               'Implement interface or event-driven solution',
                                                               'Test integration'
                                                           ];
                                                           solution.considerations = [
                                                               'Consider architectural impact',
                                                               'Maintain type safety',
                                                               'Ensure proper error handling'
                                                           ];
                                                       }

                                                   } catch (error) {
                                                       solution.error = error.message;
                                                   }

                                                   return solution;
                                               }

                                               function generateRefactoringRecommendations(cycle, graph) {
                                                   const recommendations = [];

                                                   try {
                                                       // Recommendation 1: Architecture Review
                                                       recommendations.push({
                                                           type: 'architecture_review',
                                                           priority: cycle.severity === 'critical' ? 'immediate' : 'high',
                                                           description: `Review architecture around cycle: ${cycle.path.join(' -> ')}`,
                                                           actions: [
                                                               'Analyze responsibility distribution',
                                                               'Identify Single Responsibility Principle violations',
                                                               'Consider component/service boundaries',
                                                               'Evaluate dependency direction'
                                                           ],
                                                           estimatedEffort: cycle.length > 4 ? 'high' : 'medium'
                                                       });

                                                       // Recommendation 2: Code Quality Improvement
                                                       if (cycle.length > 3) {
                                                           recommendations.push({
                                                               type: 'code_quality_improvement',
                                                               priority: 'medium',
                                                               description: 'Improve code quality to prevent future cycles',
                                                               actions: [
                                                                   'Add static analysis rules for circular dependencies',
                                                                   'Implement dependency injection best practices',
                                                                   'Add unit tests for dependency boundaries',
                                                                   'Document architectural decisions'
                                                               ],
                                                               estimatedEffort: 'medium'
                                                           });
                                                       }

                                                       // Recommendation 3: Testing Strategy
                                                       recommendations.push({
                                                           type: 'testing_strategy',
                                                           priority: 'medium',
                                                           description: 'Implement testing to prevent regression',
                                                           actions: [
                                                               'Add integration tests for cycle participants',
                                                               'Mock dependencies in unit tests',
                                                               'Test dependency injection configuration',
                                                               'Add architectural tests'
                                                           ],
                                                           estimatedEffort: 'medium'
                                                       });

                                                   } catch (error) {
                                                       recommendations.push({
                                                           type: 'error_in_recommendations',
                                                           error: error.message
                                                       });
                                                   }

                                                   return recommendations;
                                               }

                                               function generateImplementationSteps(cycles, suggestions) {
                                                   const steps = [];

                                                   try {
                                                       // Phase 1: Analysis and Planning
                                                       steps.push({
                                                           phase: 1,
                                                           name: 'analysis_and_planning',
                                                           description: 'Analyze cycles and plan resolution approach',
                                                           tasks: [
                                                               'Document all detected circular dependencies',
                                                               'Prioritize cycles by severity and impact',
                                                               'Choose resolution strategies for each cycle',
                                                               'Create implementation timeline'
                                                           ],
                                                           estimatedTime: cycles.length <= 3 ? '1-2 days' : '3-5 days',
                                                           dependencies: []
                                                       });

                                                       // Phase 2: Critical Cycle Resolution
                                                       const criticalCycles = cycles.filter(c => c.severity === 'critical');
                                                       if (criticalCycles.length > 0) {
                                                           steps.push({
                                                               phase: 2,
                                                               name: 'critical_cycle_resolution',
                                                               description: 'Resolve critical severity cycles first',
                                                               tasks: [
                                                                   'Implement dependency inversion for critical cycles',
                                                                   'Add comprehensive tests',
                                                                   'Validate resolution',
                                                                   'Deploy and monitor'
                                                               ],
                                                               estimatedTime: `${criticalCycles.length * 2} days`,
                                                               dependencies: ['analysis_and_planning']
                                                           });
                                                       }

                                                       // Phase 3: Major Cycle Resolution
                                                       const majorCycles = cycles.filter(c => c.severity === 'major');
                                                       if (majorCycles.length > 0) {
                                                           steps.push({
                                                               phase: 3,
                                                               name: 'major_cycle_resolution',
                                                               description: 'Resolve major severity cycles',
                                                               tasks: [
                                                                   'Apply chosen resolution strategies',
                                                                   'Refactor affected components/services',
                                                                   'Update tests and documentation',
                                                                   'Code review and validation'
                                                               ],
                                                               estimatedTime: `${majorCycles.length * 1.5} days`,
                                                               dependencies: criticalCycles.length > 0 ? ['critical_cycle_resolution'] : ['analysis_and_planning']
                                                           });
                                                       }

                                                       // Phase 4: Minor Cycle Resolution and Prevention
                                                       steps.push({
                                                           phase: 4,
                                                           name: 'minor_cycle_resolution_and_prevention',
                                                           description: 'Resolve remaining cycles and implement prevention',
                                                           tasks: [
                                                               'Resolve minor severity cycles',
                                                               'Implement static analysis rules',
                                                               'Add architectural tests',
                                                               'Document best practices'
                                                           ],
                                                           estimatedTime: '2-3 days',
                                                           dependencies: ['analysis_and_planning']
                                                       });

                                                   } catch (error) {
                                                       steps.push({
                                                           phase: 'error',
                                                           error: error.message
                                                       });
                                                   }

                                                   return steps;
                                               }

                                               function removeDuplicateStrategies(strategies) {
                                                   const seen = new Set();
                                                   return strategies.filter(strategy => {
                                                       if (seen.has(strategy.name)) {
                                                           return false;
                                                       }
                                                       seen.add(strategy.name);
                                                       return true;
                                                   });
                                               }

                                               function removeDuplicateSolutions(solutions) {
                                                   const seen = new Set();
                                                   return solutions.filter(solution => {
                                                       const key = `${solution.source}-${solution.target}`;
                                                       if (seen.has(key)) {
                                                           return false;
                                                       }
                                                       seen.add(key);
                                                       return true;
                                                   });
                                               }

                                               function analyzeArchitecturalImpact(cycles, graph) {
                                                   const impact = {
                                                       overallImpact: 'low',
                                                       affectedArchitecturalLayers: [],
                                                       maintainabilityScore: 100,
                                                       testabilityImpact: 'low',
                                                       scalabilityImpact: 'low',
                                                       recommendations: []
                                                   };

                                                   try {
                                                       const totalCycles = cycles.length;
                                                       const criticalCycles = cycles.filter(c => c.severity === 'critical').length;
                                                       const majorCycles = cycles.filter(c => c.severity === 'major').length;

                                                       // Assess overall impact
                                                       if (criticalCycles > 0) {
                                                           impact.overallImpact = 'high';
                                                       } else if (majorCycles > 2) {
                                                           impact.overallImpact = 'medium';
                                                       } else if (totalCycles > 3) {
                                                           impact.overallImpact = 'medium';
                                                       }

                                                       // Identify affected architectural layers
                                                       const layerImpact = new Set();
                                                       for (const cycle of cycles) {
                                                           for (const nodeId of cycle.path) {
                                                               const node = graph.nodes.find(n => n.id === nodeId);
                                                               if (node) {
                                                                   if (node.type === 'component') {
                                                                       layerImpact.add('presentation_layer');
                                                                   } else if (node.type === 'service') {
                                                                       layerImpact.add('business_logic_layer');
                                                                   } else if (node.type === 'module') {
                                                                       layerImpact.add('module_layer');
                                                                   }
                                                               }
                                                           }
                                                       }
                                                       impact.affectedArchitecturalLayers = Array.from(layerImpact);

                                                       // Calculate maintainability score
                                                       let maintainabilityScore = 100;
                                                       maintainabilityScore -= criticalCycles * 20;
                                                       maintainabilityScore -= majorCycles * 10;
                                                       maintainabilityScore -= (totalCycles - criticalCycles - majorCycles) * 5;
                                                       impact.maintainabilityScore = Math.max(0, maintainabilityScore);

                                                       // Assess testability impact
                                                       if (criticalCycles > 0 || majorCycles > 2) {
                                                           impact.testabilityImpact = 'high';
                                                       } else if (majorCycles > 0 || totalCycles > 3) {
                                                           impact.testabilityImpact = 'medium';
                                                       }

                                                       // Assess scalability impact
                                                       if (layerImpact.size > 2 || criticalCycles > 0) {
                                                           impact.scalabilityImpact = 'high';
                                                       } else if (majorCycles > 1) {
                                                           impact.scalabilityImpact = 'medium';
                                                       }

                                                       // Generate recommendations
                                                       if (impact.overallImpact === 'high') {
                                                           impact.recommendations.push({
                                                               type: 'urgent_architectural_review',
                                                               description: 'Conduct immediate architectural review and refactoring',
                                                               priority: 'critical'
                                                           });
                                                       }

                                                       if (layerImpact.size > 2) {
                                                           impact.recommendations.push({
                                                               type: 'layer_separation',
                                                               description: 'Improve separation of concerns between architectural layers',
                                                               priority: 'high'
                                                           });
                                                       }

                                                       if (impact.testabilityImpact === 'high') {
                                                           impact.recommendations.push({
                                                               type: 'testing_strategy_improvement',
                                                               description: 'Improve testing strategy to handle circular dependencies',
                                                               priority: 'medium'
                                                           });
                                                       }

                                                   } catch (error) {
                                                       impact.error = error.message;
                                                   }

                                                   return impact;
                                               }

                                               function analyzePerformanceImpact(cycles, graph) {
                                                   const analysis = {
                                                       overallPerformanceImpact: 'low',
                                                       memoryLeakRisk: 'low',
                                                       startupTimeImpact: 'low',
                                                       runtimePerformanceImpact: 'low',
                                                       changeDetectionImpact: 'low',
                                                       recommendations: []
                                                   };

                                                   try {
                                                       const totalCycles = cycles.length;
                                                       const criticalCycles = cycles.filter(c => c.severity === 'critical').length;
                                                       const componentCycles = cycles.filter(c => c.type && c.type.includes('component')).length;
                                                       const serviceCycles = cycles.filter(c => c.type && c.type.includes('service')).length;

                                                       // Assess memory leak risk
                                                       if (criticalCycles > 0) {
                                                           analysis.memoryLeakRisk = 'high';
                                                       } else if (totalCycles > 3) {
                                                           analysis.memoryLeakRisk = 'medium';
                                                       }

                                                       // Assess startup time impact
                                                       if (serviceCycles > 2) {
                                                           analysis.startupTimeImpact = 'medium';
                                                       }
                                                       if (criticalCycles > 0) {
                                                           analysis.startupTimeImpact = 'high';
                                                       }

                                                       // Assess runtime performance impact
                                                       if (componentCycles > 1) {
                                                           analysis.runtimePerformanceImpact = 'medium';
                                                       }
                                                       if (criticalCycles > 0) {
                                                           analysis.runtimePerformanceImpact = 'high';
                                                       }

                                                       // Assess change detection impact (Angular-specific)
                                                       if (componentCycles > 0) {
                                                           analysis.changeDetectionImpact = 'medium';
                                                       }
                                                       if (criticalCycles > 0 && componentCycles > 0) {
                                                           analysis.changeDetectionImpact = 'high';
                                                       }

                                                       // Overall performance impact
                                                       const impactScores = [
                                                           analysis.memoryLeakRisk === 'high' ? 3 : analysis.memoryLeakRisk === 'medium' ? 2 : 1,
                                                           analysis.startupTimeImpact === 'high' ? 3 : analysis.startupTimeImpact === 'medium' ? 2 : 1,
                                                           analysis.runtimePerformanceImpact === 'high' ? 3 : analysis.runtimePerformanceImpact === 'medium' ? 2 : 1,
                                                           analysis.changeDetectionImpact === 'high' ? 3 : analysis.changeDetectionImpact === 'medium' ? 2 : 1
                                                       ];
                                                       const avgImpact = impactScores.reduce((sum, score) => sum + score, 0) / impactScores.length;
                                                       
                                                       if (avgImpact >= 2.5) {
                                                           analysis.overallPerformanceImpact = 'high';
                                                       } else if (avgImpact >= 1.8) {
                                                           analysis.overallPerformanceImpact = 'medium';
                                                       }

                                                       // Generate performance recommendations
                                                       if (analysis.memoryLeakRisk === 'high') {
                                                           analysis.recommendations.push({
                                                               type: 'memory_leak_prevention',
                                                               description: 'Implement proper cleanup in components to prevent memory leaks',
                                                               impact: 'Reduced memory usage and improved stability'
                                                           });
                                                       }

                                                       if (analysis.changeDetectionImpact === 'high') {
                                                           analysis.recommendations.push({
                                                               type: 'change_detection_optimization',
                                                               description: 'Use OnPush change detection strategy and break component dependency cycles',
                                                               impact: 'Improved rendering performance and reduced CPU usage'
                                                           });
                                                       }

                                                       if (analysis.startupTimeImpact === 'high') {
                                                           analysis.recommendations.push({
                                                               type: 'dependency_injection_optimization',
                                                               description: 'Optimize service dependency chains and consider lazy loading',
                                                               impact: 'Faster application startup time'
                                                           });
                                                       }

                                                   } catch (error) {
                                                       analysis.error = error.message;
                                                   }

                                                   return analysis;
                                               }

                                               function generatePreventionRecommendations(circularAnalysis, cycleClassification) {
                                                   const recommendations = {
                                                       staticAnalysisTools: [],
                                                       developmentPractices: [],
                                                       architecturalGuidelines: [],
                                                       toolingRecommendations: []
                                                   };

                                                   try {
                                                       // Static Analysis Tools
                                                       recommendations.staticAnalysisTools.push({
                                                           tool: 'dependency-cruiser',
                                                           description: 'Detect circular dependencies during build time',
                                                           configuration: 'Add rules to detect and prevent circular dependencies',
                                                           benefit: 'Early detection before deployment'
                                                       });

                                                       recommendations.staticAnalysisTools.push({
                                                           tool: 'eslint-plugin-import',
                                                           description: 'Lint rules for import/export patterns',
                                                           configuration: 'Enable import/no-cycle rule',
                                                           benefit: 'IDE integration and real-time feedback'
                                                       });

                                                       recommendations.staticAnalysisTools.push({
                                                           tool: 'angular-dependency-graph',
                                                           description: 'Visualize Angular application dependencies',
                                                           configuration: 'Generate dependency graphs for analysis',
                                                           benefit: 'Visual identification of potential issues'
                                                       });

                                                       // Development Practices
                                                       recommendations.developmentPractices.push({
                                                           practice: 'dependency_direction_principle',
                                                           description: 'Establish clear dependency direction rules',
                                                           implementation: 'Define architectural layers and allowed dependency directions',
                                                           benefit: 'Prevents accidental circular dependencies'
                                                       });

                                                       recommendations.developmentPractices.push({
                                                           practice: 'interface_segregation',
                                                           description: 'Use interfaces to decouple dependencies',
                                                           implementation: 'Create focused interfaces for service contracts',
                                                           benefit: 'Reduced coupling and improved testability'
                                                       });

                                                       recommendations.developmentPractices.push({
                                                           practice: 'composition_over_inheritance',
                                                           description: 'Prefer composition to avoid inheritance cycles',
                                                           implementation: 'Use dependency injection and composition patterns',
                                                           benefit: 'More flexible and maintainable code'
                                                       });

                                                       // Architectural Guidelines
                                                       recommendations.architecturalGuidelines.push({
                                                           guideline: 'layered_architecture',
                                                           description: 'Implement clear architectural layers',
                                                           rules: [
                                                               'Presentation layer depends only on business layer',
                                                               'Business layer depends only on data layer',
                                                               'No reverse dependencies allowed'
                                                           ],
                                                           benefit: 'Natural prevention of circular dependencies'
                                                       });

                                                       recommendations.architecturalGuidelines.push({
                                                           guideline: 'module_boundaries',
                                                           description: 'Define clear module boundaries and contracts',
                                                           rules: [
                                                               'Modules should have well-defined public APIs',
                                                               'Inter-module communication through interfaces',
                                                               'Avoid deep module hierarchies'
                                                           ],
                                                           benefit: 'Improved modularity and reduced coupling'
                                                       });

                                                       // Tooling Recommendations
                                                       recommendations.toolingRecommendations.push({
                                                           tool: 'nx_workspace',
                                                           description: 'Use Nx for better dependency management',
                                                           features: [
                                                               'Dependency graph visualization',
                                                               'Module boundary enforcement',
                                                               'Affected testing and building'
                                                           ],
                                                           benefit: 'Enterprise-grade dependency management'
                                                       });

                                                       recommendations.toolingRecommendations.push({
                                                           tool: 'angular_devtools',
                                                           description: 'Use Angular DevTools for runtime analysis',
                                                           features: [
                                                               'Component tree inspection',
                                                               'Service dependency analysis',
                                                               'Performance profiling'
                                                           ],
                                                           benefit: 'Runtime visibility into dependencies'
                                                       });

                                                   } catch (error) {
                                                       recommendations.error = error.message;
                                                   }

                                                   return recommendations;
                                               }

                                               function calculateResolutionComplexity(cycles) {
                                                   if (cycles.length === 0) return 'none';
                                                   
                                                   const complexityScore = cycles.reduce((sum, cycle) => {
                                                       let score = cycle.length; // Base complexity from cycle length
                                                       
                                                       // Add complexity based on severity
                                                       if (cycle.severity === 'critical') {
                                                           score *= 3;
                                                       } else if (cycle.severity === 'major') {
                                                           score *= 2;
                                                       }
                                                       
                                                       // Add complexity based on type
                                                       if (cycle.type === 'mixed_architecture_cycle') {
                                                           score *= 1.5;
                                                       }
                                                       
                                                       return sum + score;
                                                   }, 0);
                                                   
                                                   const avgComplexity = complexityScore / cycles.length;
                                                   
                                                   if (avgComplexity >= 12) {
                                                       return 'very_high';
                                                   } else if (avgComplexity >= 8) {
                                                       return 'high';
                                                   } else if (avgComplexity >= 5) {
                                                       return 'medium';
                                                   } else {
                                                       return 'low';
                                                   }
                                               }

                                           })();
                                       
                           """;

            var analysisResult = await session.Page.EvaluateAsync<object>(jsCode);

            return JsonSerializer.Serialize(analysisResult, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                timestamp = DateTime.UtcNow.ToString("O"),
                sessionId,
                analysisType = "circular_dependency_detection",
                recommendation = "Ensure the page has an Angular application loaded and try enabling development mode for better analysis"
            }, JsonOptions);
        }
    }
}
