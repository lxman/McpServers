using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Playwright.Core.Services;

namespace PlaywrightServerMcp.Tools;

/// <summary>
/// Handles Angular component detection, architecture analysis, and component tree building
/// Implements ANG-001 Enhanced Angular Detection Foundation
/// Implements ANG-008 Standalone Component Validation
/// </summary>
[McpServerToolType]
public class AngularComponentAnalyzer(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Enhanced Angular component hierarchy analysis with Angular 17+ support, standalone components, and signals detection")]
    public async Task<string> GetAngularComponentTree(
        [Description("Session ID")] string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            // Enhanced JavaScript for Angular 17+ detection with modern features (FIXED CSS SELECTORS)
            var jsCode = @"
                (() => {
                    // Initialize result structure
                    const results = {
                        angularDetected: false,
                        angularVersion: null,
                        modernFeatures: {
                            standaloneComponents: [],
                            signalsDetected: false,
                            signalsUsage: [],
                            zonelessApp: false,
                            newControlFlow: false
                        },
                        componentTree: [],
                        architecture: {
                            totalComponents: 0,
                            standaloneCount: 0,
                            moduleBasedCount: 0,
                            signalsCount: 0
                        },
                        performance: {
                            changeDetectionStrategy: {},
                            asyncOperations: []
                        }
                    };

                    // Step 1: Enhanced Angular Detection (FIXED)
                    const detectAngular = () => {
                        // Check for Angular global objects (Angular 17+ has enhanced globals)
                        const hasAngularGlobal = !!(window.ng || window.ngDevMode || window.getAllAngularRootElements);
                        
                        // Check for Angular DevTools hooks (Angular 17+ specific)
                        const hasDevTools = !!(window.ngDevMode || window.ng?.getComponent || window.ng?.applyChanges);
                        
                        // FIXED: Check for Angular elements in DOM using proper selectors
                        let hasNgElements = false;
                        
                        // Check for ng-version attribute
                        const versionElements = document.querySelectorAll('[ng-version]');
                        if (versionElements.length > 0) hasNgElements = true;
                        
                        // Check for Angular host and content attributes by iterating through elements
                        if (!hasNgElements) {
                            const allElements = Array.from(document.querySelectorAll('*')).slice(0, 200); // Limit for performance
                            hasNgElements = allElements.some(el => {
                                return Array.from(el.attributes).some(attr => 
                                    attr.name.startsWith('_nghost-') || 
                                    attr.name.startsWith('_ngcontent-')
                                );
                            });
                        }
                        
                        // Check for Angular-specific attributes
                        const angularAttributes = [];
                        Array.from(document.querySelectorAll('*')).slice(0, 100).forEach(el => {
                            Array.from(el.attributes).forEach(attr => {
                                if (attr.name.startsWith('ng-') || 
                                    attr.name.startsWith('_ng') ||
                                    attr.name.includes('ng-reflect') ||
                                    attr.name.includes('_nghost') ||
                                    attr.name.includes('_ngcontent')) {
                                    angularAttributes.push(attr.name);
                                }
                            });
                        });
                        
                        results.angularDetected = hasAngularGlobal || hasDevTools || hasNgElements || angularAttributes.length > 0;
                        
                        // Try to determine Angular version
                        if (results.angularDetected) {
                            // Check ng-version attribute
                            const versionElement = document.querySelector('[ng-version]');
                            if (versionElement) {
                                results.angularVersion = versionElement.getAttribute('ng-version');
                            }
                            
                            // Alternative version detection through Angular DevTools
                            if (!results.angularVersion && window.ng?.version) {
                                results.angularVersion = window.ng.version.full;
                            }
                        }
                        
                        return results.angularDetected;
                    };

                    // Step 2: Detect Standalone Components (Angular 14+ feature, enhanced in 17+)
                    const detectStandaloneComponents = () => {
                        const standaloneComponents = [];
                        
                        // Method 1: Check for components with standalone metadata
                        if (window.ng?.getComponent) {
                            try {
                                const allElements = Array.from(document.querySelectorAll('*'));
                                allElements.forEach(el => {
                                    try {
                                        const component = window.ng.getComponent(el);
                                        if (component && component.constructor) {
                                            // Try to detect if it's a standalone component
                                            const componentMetadata = component.constructor;
                                            
                                            // Look for standalone indicators
                                            const isStandalone = componentMetadata.ɵcmp?.standalone === true ||
                                                               componentMetadata.standalone === true;
                                            
                                            if (isStandalone) {
                                                standaloneComponents.push({
                                                    tagName: el.tagName.toLowerCase(),
                                                    selector: el.tagName.toLowerCase(),
                                                    componentName: componentMetadata.name || 'Unknown',
                                                    hasImports: !!(componentMetadata.ɵcmp?.imports),
                                                    standalone: true
                                                });
                                            }
                                        }
                                    } catch (e) {
                                        // Skip elements that don't have components
                                    }
                                });
                            } catch (e) {
                                console.warn('Could not access component metadata:', e);
                            }
                        }
                        
                        // Method 2: Heuristic detection based on DOM patterns
                        const customElements = Array.from(document.querySelectorAll('*'))
                            .filter(el => {
                                const tagName = el.tagName.toLowerCase();
                                return tagName.includes('-') && // Custom elements typically have hyphens
                                       !tagName.startsWith('mat-') && // Exclude Material components
                                       !tagName.startsWith('p-') && // Exclude PrimeNG
                                       !tagName.startsWith('nz-') && // Exclude NG-ZORRO
                                       Array.from(el.attributes).some(attr => 
                                           attr.name.startsWith('_ng') || attr.name.startsWith('ng-'));
                            });
                        
                        customElements.forEach(el => {
                            // Additional heuristics for standalone detection
                            const hasModernAttributes = Array.from(el.attributes).some(attr =>
                                attr.name.includes('standalone') || 
                                attr.name.includes('imports'));
                            
                            if (hasModernAttributes) {
                                standaloneComponents.push({
                                    tagName: el.tagName.toLowerCase(),
                                    selector: el.tagName.toLowerCase(),
                                    componentName: 'Detected via DOM patterns',
                                    detectionMethod: 'heuristic',
                                    standalone: true
                                });
                            }
                        });
                        
                        results.modernFeatures.standaloneComponents = standaloneComponents;
                        results.architecture.standaloneCount = standaloneComponents.length;
                    };

                    // Step 3: Detect Signals Usage (Angular 16+ feature, core in 17+)
                    const detectSignals = () => {
                        const signalsUsage = [];
                        
                        // Method 1: Check for signals in component instances
                        if (window.ng?.getComponent) {
                            try {
                                const allElements = Array.from(document.querySelectorAll('*'));
                                allElements.forEach(el => {
                                    try {
                                        const component = window.ng.getComponent(el);
                                        if (component) {
                                            // Look for signal properties
                                            const signalProperties = [];
                                            
                                            // Check component properties for signals
                                            Object.getOwnPropertyNames(component).forEach(prop => {
                                                try {
                                                    const value = component[prop];
                                                    // Angular signals have specific characteristics
                                                    if (value && typeof value === 'function' && 
                                                        (value.ɵIsSignal || 
                                                         (value.constructor && value.constructor.name === 'SignalImpl') ||
                                                         (typeof value.set === 'function' && typeof value.update === 'function'))) {
                                                        signalProperties.push({
                                                            property: prop,
                                                            type: 'signal',
                                                            currentValue: value() // Get signal value
                                                        });
                                                    }
                                                } catch (e) {
                                                    // Skip properties that can't be accessed
                                                }
                                            });
                                            
                                            if (signalProperties.length > 0) {
                                                signalsUsage.push({
                                                    element: el.tagName.toLowerCase(),
                                                    componentName: component.constructor.name,
                                                    signals: signalProperties
                                                });
                                            }
                                        }
                                    } catch (e) {
                                        // Skip elements without components
                                    }
                                });
                            } catch (e) {
                                console.warn('Could not check for signals:', e);
                            }
                        }
                        
                        // Method 2: Check for signals in global scope (if exposed)
                        if (window.ng && typeof window.ng.signal === 'function') {
                            results.modernFeatures.signalsDetected = true;
                        }
                        
                        // Method 3: Look for signal-related text in script tags (development mode)
                        const scripts = Array.from(document.querySelectorAll('script'));
                        const hasSignalCode = scripts.some(script => {
                            const text = script.textContent || '';
                            return text.includes('signal(') || 
                                   text.includes('computed(') || 
                                   text.includes('effect(') ||
                                   text.includes('SignalImpl');
                        });
                        
                        if (hasSignalCode || signalsUsage.length > 0) {
                            results.modernFeatures.signalsDetected = true;
                        }
                        
                        results.modernFeatures.signalsUsage = signalsUsage;
                        results.architecture.signalsCount = signalsUsage.reduce((count, usage) => 
                            count + usage.signals.length, 0);
                    };

                    // Step 4: Check for new Angular 17+ features
                    const detectModernFeatures = () => {
                        // Check for zoneless application (Angular 18+ experimental)
                        const hasZonelessIndicator = !window.Zone || 
                                                   document.querySelector('[ng-zoneless]') ||
                                                   (window.ng && window.ng.experimental?.provideZonelessChangeDetection);
                        results.modernFeatures.zonelessApp = hasZonelessIndicator;
                        
                        // Check for new control flow (@if, @for, @switch - Angular 17+)
                        const hasNewControlFlow = Array.from(document.querySelectorAll('*')).some(el => {
                            const content = el.innerHTML;
                            return content.includes('@if') || 
                                   content.includes('@for') || 
                                   content.includes('@switch') ||
                                   content.includes('@defer');
                        });
                        results.modernFeatures.newControlFlow = hasNewControlFlow;
                    };

                    // Step 5: Build Component Tree (FIXED)
                    const buildComponentTree = () => {
                        const componentTree = [];
                        
                        // FIXED: Find all Angular components using proper attribute detection
                        const componentElements = Array.from(document.querySelectorAll('*'))
                            .filter(el => {
                                // Look for Angular component indicators
                                return Array.from(el.attributes).some(attr => 
                                    attr.name.startsWith('_nghost') || 
                                    attr.name.startsWith('ng-reflect') ||
                                    (attr.name.startsWith('_ng') && attr.name.includes('c')));
                            });
                        
                        componentElements.forEach((el, index) => {
                            const component = {
                                index: index,
                                tagName: el.tagName.toLowerCase(),
                                selector: el.tagName.toLowerCase(),
                                attributes: Array.from(el.attributes)
                                    .filter(attr => attr.name.startsWith('ng-') || attr.name.startsWith('_ng'))
                                    .map(attr => ({ name: attr.name, value: attr.value })),
                                position: {
                                    top: el.offsetTop,
                                    left: el.offsetLeft,
                                    width: el.offsetWidth,
                                    height: el.offsetHeight
                                },
                                children: []
                            };
                            
                            // Check if this is a standalone component
                            const isStandalone = results.modernFeatures.standaloneComponents
                                .some(sc => sc.selector === component.selector);
                            component.standalone = isStandalone;
                            
                            // Check for change detection strategy
                            const onPushAttribute = el.getAttribute('ng-reflect-change-detection-strategy');
                            if (onPushAttribute) {
                                component.changeDetectionStrategy = onPushAttribute;
                            }
                            
                            componentTree.push(component);
                        });
                        
                        results.componentTree = componentTree;
                        results.architecture.totalComponents = componentTree.length;
                        results.architecture.moduleBasedCount = componentTree.length - results.architecture.standaloneCount;
                    };

                    // Step 6: Performance Analysis
                    const analyzePerformance = () => {
                        const strategies = {};
                        
                        results.componentTree.forEach(comp => {
                            const strategy = comp.changeDetectionStrategy || 'Default';
                            strategies[strategy] = (strategies[strategy] || 0) + 1;
                        });
                        
                        results.performance.changeDetectionStrategy = strategies;
                        
                        // Detect async operations
                        const asyncOps = [];
                        if (window.Zone && window.Zone.current) {
                            // This is a simplified check - real monitoring would need more setup
                            asyncOps.push({
                                type: 'zone-detected',
                                description: 'Zone.js is active, change detection will trigger on async operations'
                            });
                        }
                        
                        results.performance.asyncOperations = asyncOps;
                    };

                    // Execute detection steps
                    if (!detectAngular()) {
                        return 'Angular application not detected on this page';
                    }
                    
                    detectStandaloneComponents();
                    detectSignals();
                    detectModernFeatures();
                    buildComponentTree();
                    analyzePerformance();
                    
                    return results;
                })();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to get Angular component tree: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Validate standalone components' import patterns and dependency validation - ANG-008 Implementation")]
    public async Task<string> ValidateStandaloneDependencies(
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
                        validationSummary: {
                            angularDetected: false,
                            angularVersion: null,
                            standaloneComponentsFound: 0,
                            validationTimestamp: new Date().toISOString(),
                            overallStatus: 'not_analyzed'
                        },
                        standaloneComponents: [],
                        importPatternAnalysis: {
                            totalImports: 0,
                            commonModules: [],
                            importPatterns: [],
                            potentialIssues: []
                        },
                        dependencyValidation: {
                            missingDependencies: [],
                            unusedImports: [],
                            circularDependencies: [],
                            duplicateImports: [],
                            importConflicts: []
                        },
                        bestPracticesAssessment: {
                            followsBestPractices: [],
                            violations: [],
                            recommendations: []
                        },
                        performanceImpact: {
                            bundleSizeImpact: 'unknown',
                            treeShakingEfficiency: 'unknown',
                            lazyLoadingCompatibility: 'unknown'
                        },
                        migrationAssessment: {
                            moduleToStandaloneReadiness: 'unknown',
                            blockers: [],
                            migrationSteps: []
                        }
                    };

                    // Step 1: Enhanced Angular Detection
                    const detectAngular = () => {
                        const hasAngularGlobal = !!(window.ng || window.ngDevMode || window.getAllAngularRootElements);
                        const hasDevTools = !!(window.ngDevMode || window.ng?.getComponent || window.ng?.applyChanges);
                        
                        let hasNgElements = false;
                        const versionElements = document.querySelectorAll('[ng-version]');
                        if (versionElements.length > 0) hasNgElements = true;
                        
                        if (!hasNgElements) {
                            const allElements = Array.from(document.querySelectorAll('*')).slice(0, 100);
                            hasNgElements = allElements.some(el => {
                                return Array.from(el.attributes).some(attr => 
                                    attr.name.startsWith('_nghost-') || 
                                    attr.name.startsWith('_ngcontent-')
                                );
                            });
                        }
                        
                        results.validationSummary.angularDetected = hasAngularGlobal || hasDevTools || hasNgElements;
                        
                        if (results.validationSummary.angularDetected) {
                            const versionElement = document.querySelector('[ng-version]');
                            if (versionElement) {
                                results.validationSummary.angularVersion = versionElement.getAttribute('ng-version');
                            }
                        }
                        
                        return results.validationSummary.angularDetected;
                    };

                    // Step 2: Comprehensive Standalone Component Discovery
                    const discoverStandaloneComponents = () => {
                        const standaloneComponents = [];
                        
                        // Method 1: DevTools-based detection (most accurate)
                        if (window.ng?.getComponent) {
                            try {
                                const allElements = Array.from(document.querySelectorAll('*'));
                                allElements.forEach(el => {
                                    try {
                                        const component = window.ng.getComponent(el);
                                        if (component && component.constructor) {
                                            const componentMetadata = component.constructor;
                                            const componentDef = componentMetadata.ɵcmp;
                                            
                                            // Check if component is standalone
                                            const isStandalone = componentDef?.standalone === true ||
                                                               componentMetadata.standalone === true;
                                            
                                            if (isStandalone) {
                                                const componentInfo = {
                                                    element: el,
                                                    selector: el.tagName.toLowerCase(),
                                                    componentName: componentMetadata.name || 'Unknown',
                                                    componentClass: componentMetadata,
                                                    detectionMethod: 'devtools',
                                                    metadata: {
                                                        imports: componentDef?.imports || [],
                                                        declarations: componentDef?.declarations || [],
                                                        providers: componentDef?.providers || [],
                                                        schemas: componentDef?.schemas || []
                                                    },
                                                    instanceProperties: Object.getOwnPropertyNames(component)
                                                        .filter(prop => !prop.startsWith('__'))
                                                        .slice(0, 50) // Limit for performance
                                                };
                                                
                                                standaloneComponents.push(componentInfo);
                                            }
                                        }
                                    } catch (e) {
                                        // Skip elements that don't have components
                                    }
                                });
                            } catch (e) {
                                console.warn('Could not use DevTools detection:', e);
                            }
                        }
                        
                        // Method 2: Heuristic detection for production builds
                        if (standaloneComponents.length === 0) {
                            const customElements = Array.from(document.querySelectorAll('*'))
                                .filter(el => {
                                    const tagName = el.tagName.toLowerCase();
                                    return tagName.includes('-') &&
                                           !tagName.startsWith('mat-') &&
                                           !tagName.startsWith('p-') &&
                                           !tagName.startsWith('nz-') &&
                                           Array.from(el.attributes).some(attr => 
                                               attr.name.startsWith('_ng') || attr.name.startsWith('ng-'));
                                });
                            
                            customElements.forEach(el => {
                                standaloneComponents.push({
                                    element: el,
                                    selector: el.tagName.toLowerCase(),
                                    componentName: 'Detected via heuristics',
                                    detectionMethod: 'heuristic',
                                    metadata: {
                                        imports: [],
                                        declarations: [],
                                        providers: [],
                                        schemas: []
                                    },
                                    instanceProperties: []
                                });
                            });
                        }
                        
                        return standaloneComponents;
                    };

                    // Step 3: Analyze Import Patterns
                    const analyzeImportPatterns = (standaloneComponents) => {
                        const importAnalysis = {
                            totalImports: 0,
                            commonModules: {},
                            importPatterns: [],
                            potentialIssues: []
                        };
                        
                        standaloneComponents.forEach(comp => {
                            const imports = comp.metadata.imports || [];
                            importAnalysis.totalImports += imports.length;
                            
                            // Analyze import types
                            imports.forEach(imp => {
                                try {
                                    const importName = imp.name || imp.constructor?.name || 'Unknown';
                                    
                                    // Track common modules
                                    if (!importAnalysis.commonModules[importName]) {
                                        importAnalysis.commonModules[importName] = {
                                            count: 0,
                                            components: [],
                                            type: 'unknown'
                                        };
                                    }
                                    
                                    importAnalysis.commonModules[importName].count++;
                                    importAnalysis.commonModules[importName].components.push(comp.componentName);
                                    
                                    // Categorize import types
                                    if (importName.includes('Common') || importName.includes('FormsModule') || 
                                        importName.includes('ReactiveFormsModule') || importName.includes('HttpClientModule')) {
                                        importAnalysis.commonModules[importName].type = 'angular-core';
                                    } else if (importName.includes('Material') || importName.startsWith('Mat')) {
                                        importAnalysis.commonModules[importName].type = 'angular-material';
                                    } else if (importName.includes('Router')) {
                                        importAnalysis.commonModules[importName].type = 'angular-router';
                                    } else {
                                        importAnalysis.commonModules[importName].type = 'custom';
                                    }
                                } catch (e) {
                                    importAnalysis.potentialIssues.push({
                                        component: comp.componentName,
                                        issue: 'Could not analyze import',
                                        details: e.message,
                                        severity: 'low'
                                    });
                                }
                            });
                        });
                        
                        // Identify patterns
                        Object.entries(importAnalysis.commonModules).forEach(([moduleName, info]) => {
                            if (info.count > 1) {
                                importAnalysis.importPatterns.push({
                                    pattern: 'shared-module',
                                    module: moduleName,
                                    usage: info.count,
                                    recommendation: info.count > 3 ? 
                                        'Consider creating a shared import array or feature module' :
                                        'Acceptable usage pattern'
                                });
                            }
                            
                            if (info.type === 'angular-core' && info.count === standaloneComponents.length) {
                                importAnalysis.importPatterns.push({
                                    pattern: 'universal-core-import',
                                    module: moduleName,
                                    recommendation: 'Consider bootstrapping with this module globally'
                                });
                            }
                        });
                        
                        return importAnalysis;
                    };

                    // Step 4: Dependency Validation
                    const validateDependencies = (standaloneComponents) => {
                        const validation = {
                            missingDependencies: [],
                            unusedImports: [],
                            circularDependencies: [],
                            duplicateImports: [],
                            importConflicts: []
                        };
                        
                        standaloneComponents.forEach(comp => {
                            // Check for potential missing dependencies
                            const usedFeatures = [];
                            
                            // Analyze component properties for Angular feature usage
                            comp.instanceProperties.forEach(prop => {
                                if (prop.includes('form') || prop.includes('Form')) {
                                    usedFeatures.push('FormsModule');
                                }
                                if (prop.includes('http') || prop.includes('Http')) {
                                    usedFeatures.push('HttpClientModule');
                                }
                                if (prop.includes('router') || prop.includes('Router')) {
                                    usedFeatures.push('RouterModule');
                                }
                            });
                            
                            // Check if required modules are imported
                            const importNames = (comp.metadata.imports || [])
                                .map(imp => imp.name || imp.constructor?.name || 'Unknown');
                            
                            usedFeatures.forEach(feature => {
                                if (!importNames.some(imp => imp.includes(feature.replace('Module', '')))) {
                                    validation.missingDependencies.push({
                                        component: comp.componentName,
                                        missingModule: feature,
                                        evidence: `Component properties suggest ${feature} usage`,
                                        severity: 'medium',
                                        recommendation: `Add ${feature} to component imports`
                                    });
                                }
                            });
                            
                            // Check for duplicate imports (same type imported multiple times)
                            const importCounts = {};
                            importNames.forEach(name => {
                                importCounts[name] = (importCounts[name] || 0) + 1;
                            });
                            
                            Object.entries(importCounts).forEach(([name, count]) => {
                                if (count > 1) {
                                    validation.duplicateImports.push({
                                        component: comp.componentName,
                                        duplicateModule: name,
                                        count: count,
                                        severity: 'low',
                                        recommendation: 'Remove duplicate imports'
                                    });
                                }
                            });
                        });
                        
                        // Check for cross-component dependency issues
                        const allImports = standaloneComponents.flatMap(comp => 
                            (comp.metadata.imports || []).map(imp => ({
                                component: comp.componentName,
                                import: imp.name || imp.constructor?.name || 'Unknown'
                            }))
                        );
                        
                        // Simple circular dependency check (limited scope)
                        const componentNames = standaloneComponents.map(c => c.componentName);
                        allImports.forEach(impInfo => {
                            if (componentNames.includes(impInfo.import) && impInfo.import !== impInfo.component) {
                                validation.circularDependencies.push({
                                    component: impInfo.component,
                                    dependsOn: impInfo.import,
                                    type: 'component-to-component',
                                    severity: 'high',
                                    recommendation: 'Consider using services or shared state management'
                                });
                            }
                        });
                        
                        return validation;
                    };

                    // Step 5: Best Practices Assessment
                    const assessBestPractices = (standaloneComponents, importAnalysis) => {
                        const assessment = {
                            followsBestPractices: [],
                            violations: [],
                            recommendations: []
                        };
                        
                        standaloneComponents.forEach(comp => {
                            const imports = comp.metadata.imports || [];
                            
                            // Check import count (too many imports can indicate design issues)
                            if (imports.length === 0) {
                                assessment.violations.push({
                                    component: comp.componentName,
                                    violation: 'No imports detected',
                                    severity: 'low',
                                    explanation: 'Standalone components typically need at least CommonModule',
                                    recommendation: 'Ensure component has necessary imports'
                                });
                            } else if (imports.length > 15) {
                                assessment.violations.push({
                                    component: comp.componentName,
                                    violation: 'Too many imports',
                                    severity: 'medium',
                                    count: imports.length,
                                    explanation: 'Components with many imports may benefit from feature modules',
                                    recommendation: 'Consider grouping related imports into feature modules'
                                });
                            } else {
                                assessment.followsBestPractices.push({
                                    component: comp.componentName,
                                    practice: 'Reasonable import count',
                                    details: `${imports.length} imports - within recommended range`
                                });
                            }
                            
                            // Check for recommended patterns
                            const hasCommonModule = imports.some(imp => 
                                (imp.name || '').includes('Common') || 
                                (imp.constructor?.name || '').includes('Common')
                            );
                            
                            if (hasCommonModule) {
                                assessment.followsBestPractices.push({
                                    component: comp.componentName,
                                    practice: 'Includes CommonModule',
                                    details: 'Following Angular best practices for standalone components'
                                });
                            } else {
                                assessment.violations.push({
                                    component: comp.componentName,
                                    violation: 'Missing CommonModule',
                                    severity: 'medium',
                                    explanation: 'CommonModule provides essential directives like ngIf, ngFor',
                                    recommendation: 'Add CommonModule to imports array'
                                });
                            }
                        });
                        
                        // Global recommendations
                        if (standaloneComponents.length > 0) {
                            assessment.recommendations.push({
                                type: 'architecture',
                                title: 'Standalone Component Strategy',
                                description: `Found ${standaloneComponents.length} standalone components`,
                                suggestions: [
                                    'Consider creating shared import arrays for common dependencies',
                                    'Use tree-shakable providers for services',
                                    'Implement lazy loading for feature components',
                                    'Monitor bundle size impact of component imports'
                                ]
                            });
                        }
                        
                        if (importAnalysis.totalImports > 50) {
                            assessment.recommendations.push({
                                type: 'performance',
                                title: 'Import Optimization',
                                description: `High import count detected (${importAnalysis.totalImports} total)`,
                                suggestions: [
                                    'Consider code splitting and lazy loading',
                                    'Evaluate if some imports can be shared globally',
                                    'Use Angular CDK primitives instead of full Material modules where possible',
                                    'Implement dynamic imports for rarely used features'
                                ]
                            });
                        }
                        
                        return assessment;
                    };

                    // Step 6: Performance Impact Assessment
                    const assessPerformanceImpact = (standaloneComponents, importAnalysis) => {
                        const impact = {
                            bundleSizeImpact: 'analyzing',
                            treeShakingEfficiency: 'analyzing',
                            lazyLoadingCompatibility: 'analyzing',
                            details: []
                        };
                        
                        // Analyze bundle size impact
                        const totalImports = importAnalysis.totalImports;
                        const uniqueModules = Object.keys(importAnalysis.commonModules).length;
                        
                        if (totalImports < 20) {
                            impact.bundleSizeImpact = 'low';
                            impact.details.push('Low import count suggests minimal bundle size impact');
                        } else if (totalImports < 50) {
                            impact.bundleSizeImpact = 'medium';
                            impact.details.push('Moderate import count - monitor for bundle size growth');
                        } else {
                            impact.bundleSizeImpact = 'high';
                            impact.details.push('High import count may significantly impact bundle size');
                        }
                        
                        // Tree shaking assessment
                        const materialImports = Object.keys(importAnalysis.commonModules)
                            .filter(name => name.includes('Material') || name.startsWith('Mat')).length;
                        
                        if (materialImports === 0) {
                            impact.treeShakingEfficiency = 'excellent';
                            impact.details.push('No Material UI detected - optimal for tree shaking');
                        } else if (materialImports < 5) {
                            impact.treeShakingEfficiency = 'good';
                            impact.details.push('Limited Material UI usage - good tree shaking potential');
                        } else {
                            impact.treeShakingEfficiency = 'moderate';
                            impact.details.push('Heavy Material UI usage - verify individual imports');
                        }
                        
                        // Lazy loading compatibility
                        const hasRouterDependency = Object.keys(importAnalysis.commonModules)
                            .some(name => name.includes('Router'));
                        
                        if (standaloneComponents.length > 0 && !hasRouterDependency) {
                            impact.lazyLoadingCompatibility = 'excellent';
                            impact.details.push('Standalone components are ideal for lazy loading');
                        } else if (standaloneComponents.length > 0) {
                            impact.lazyLoadingCompatibility = 'good';
                            impact.details.push('Standalone components with routing - verify lazy loading setup');
                        } else {
                            impact.lazyLoadingCompatibility = 'unknown';
                            impact.details.push('No standalone components detected for analysis');
                        }
                        
                        return impact;
                    };

                    // Step 7: Migration Assessment
                    const assessMigrationReadiness = (standaloneComponents) => {
                        const migration = {
                            moduleToStandaloneReadiness: 'analyzing',
                            blockers: [],
                            migrationSteps: [],
                            estimatedEffort: 'unknown'
                        };
                        
                        if (standaloneComponents.length === 0) {
                            migration.moduleToStandaloneReadiness = 'ready';
                            migration.migrationSteps = [
                                '1. Identify components suitable for standalone conversion',
                                '2. Add standalone: true to component decorator',
                                '3. Move module imports to component imports array',
                                '4. Update bootstrap configuration if needed',
                                '5. Test component isolation and functionality',
                                '6. Update routing configuration for lazy loading'
                            ];
                            migration.estimatedEffort = 'low-medium';
                        } else {
                            migration.moduleToStandaloneReadiness = 'in-progress';
                            migration.migrationSteps = [
                                '1. Continue converting remaining module-based components',
                                '2. Optimize import sharing between standalone components',
                                '3. Implement lazy loading for feature components',
                                '4. Refactor shared dependencies into feature modules if needed'
                            ];
                            migration.estimatedEffort = 'medium';
                        }
                        
                        // Check for potential blockers
                        if (typeof window.Zone !== 'undefined' && standaloneComponents.length > 0) {
                            migration.blockers.push({
                                blocker: 'Zone.js dependency',
                                description: 'Application still uses Zone.js - consider zoneless migration',
                                severity: 'low',
                                workaround: 'Continue with current setup, evaluate zoneless in future'
                            });
                        }
                        
                        return migration;
                    };

                    // Execute validation workflow
                    try {
                        if (!detectAngular()) {
                            results.validationSummary.overallStatus = 'no_angular_detected';
                            return results;
                        }
                        
                        const standaloneComponents = discoverStandaloneComponents();
                        results.validationSummary.standaloneComponentsFound = standaloneComponents.length;
                        
                        if (standaloneComponents.length === 0) {
                            results.validationSummary.overallStatus = 'no_standalone_components';
                            results.migrationAssessment = assessMigrationReadiness(standaloneComponents);
                            return results;
                        }
                        
                        // Store component data (without circular references)
                        results.standaloneComponents = standaloneComponents.map(comp => ({
                            selector: comp.selector,
                            componentName: comp.componentName,
                            detectionMethod: comp.detectionMethod,
                            importCount: comp.metadata.imports.length,
                            hasProviders: comp.metadata.providers.length > 0,
                            propertyCount: comp.instanceProperties.length
                        }));
                        
                        // Perform comprehensive analysis
                        results.importPatternAnalysis = analyzeImportPatterns(standaloneComponents);
                        results.dependencyValidation = validateDependencies(standaloneComponents);
                        results.bestPracticesAssessment = assessBestPractices(standaloneComponents, results.importPatternAnalysis);
                        results.performanceImpact = assessPerformanceImpact(standaloneComponents, results.importPatternAnalysis);
                        results.migrationAssessment = assessMigrationReadiness(standaloneComponents);
                        
                        // Determine overall status
                        const hasErrors = results.dependencyValidation.missingDependencies.length > 0 ||
                                        results.dependencyValidation.circularDependencies.length > 0;
                        const hasWarnings = results.dependencyValidation.unusedImports.length > 0 ||
                                          results.bestPracticesAssessment.violations.length > 0;
                        
                        if (hasErrors) {
                            results.validationSummary.overallStatus = 'errors_found';
                        } else if (hasWarnings) {
                            results.validationSummary.overallStatus = 'warnings_found';
                        } else {
                            results.validationSummary.overallStatus = 'validation_passed';
                        }
                        
                        return results;
                        
                    } catch (error) {
                        results.validationSummary.overallStatus = 'analysis_failed';
                        results.validationSummary.error = error.message;
                        return results;
                    }
                })();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to validate standalone dependencies: {ex.Message}";
        }
    }
}
