using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// Implements Angular Style Guide compliance checking and validation
/// Implements ANG-018 Angular Style Guide Compliance
/// Validates code against official Angular Style Guide recommendations
/// </summary>
[McpServerToolType]
public class AngularStyleGuideCompliance(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Validate Angular application against Angular Style Guide compliance rules and best practices")]
    public async Task<string> ValidateAngularStyleGuideCompliance(
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
                        complianceOverview: {
                            angularDetected: false,
                            angularVersion: null,
                            analysisTimestamp: new Date().toISOString(),
                            overallComplianceScore: 0,
                            totalChecks: 0,
                            passedChecks: 0,
                            failedChecks: 0,
                            warningChecks: 0,
                            complianceGrade: 'F'
                        },
                        namingConventions: {
                            components: {
                                checked: 0,
                                compliant: 0,
                                violations: []
                            },
                            services: {
                                checked: 0,
                                compliant: 0,
                                violations: []
                            },
                            files: {
                                checked: 0,
                                compliant: 0,
                                violations: []
                            }
                        },
                        componentArchitecture: {
                            singleResponsibility: {
                                score: 0,
                                violations: [],
                                recommendations: []
                            },
                            smallFunctions: {
                                score: 0,
                                violations: [],
                                recommendations: []
                            },
                            propertyOrdering: {
                                score: 0,
                                violations: []
                            }
                        },
                        codeOrganization: {
                            folderStructure: {
                                score: 0,
                                issues: [],
                                recommendations: []
                            },
                            featureModules: {
                                score: 0,
                                detected: false,
                                recommendations: []
                            },
                            sharedModules: {
                                score: 0,
                                detected: false,
                                recommendations: []
                            }
                        },
                        templateAndStyling: {
                            templateSyntax: {
                                score: 0,
                                violations: []
                            },
                            cssStyling: {
                                score: 0,
                                violations: []
                            },
                            accessibility: {
                                score: 0,
                                violations: []
                            }
                        },
                        servicesAndDI: {
                            singletonServices: {
                                score: 0,
                                violations: []
                            },
                            providedIn: {
                                score: 0,
                                violations: []
                            },
                            interfaceUsage: {
                                score: 0,
                                recommendations: []
                            }
                        },
                        performancePatterns: {
                            changeDetection: {
                                score: 0,
                                analysis: [],
                                recommendations: []
                            },
                            trackByFunctions: {
                                score: 0,
                                violations: []
                            },
                            lazyLoading: {
                                score: 0,
                                analysis: []
                            }
                        },
                        modernAngularPatterns: {
                            standaloneComponents: {
                                usage: 0,
                                score: 0,
                                recommendations: []
                            },
                            signals: {
                                usage: 0,
                                score: 0,
                                recommendations: []
                            },
                            controlFlow: {
                                usage: 0,
                                score: 0,
                                recommendations: []
                            }
                        },
                        detailedRecommendations: [],
                        complianceActionPlan: []
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
                        
                        results.complianceOverview.angularDetected = hasAngularGlobal || hasDevTools || hasNgElements;
                        
                        if (results.complianceOverview.angularDetected) {
                            const versionElement = document.querySelector('[ng-version]');
                            if (versionElement) {
                                results.complianceOverview.angularVersion = versionElement.getAttribute('ng-version');
                            }
                        }
                        
                        return results.complianceOverview.angularDetected;
                    };

                    // Step 2: Analyze Naming Conventions
                    const analyzeNamingConventions = () => {
                        const components = [];
                        
                        // Extract component information
                        if (window.ng?.getComponent) {
                            try {
                                const allElements = Array.from(document.querySelectorAll('*'));
                                allElements.forEach(el => {
                                    try {
                                        const component = window.ng.getComponent(el);
                                        if (component && component.constructor) {
                                            const componentName = component.constructor.name;
                                            const selector = el.tagName.toLowerCase();
                                            
                                            components.push({
                                                name: componentName,
                                                selector: selector,
                                                element: el
                                            });
                                        }
                                    } catch (e) {
                                        // Skip elements without components
                                    }
                                });
                            } catch (e) {
                                console.warn('Could not analyze components:', e);
                            }
                        }
                        
                        // Validate component naming (Style Guide: Use kebab-case for selectors)
                        components.forEach(comp => {
                            results.namingConventions.components.checked++;
                            
                            // Check selector naming (should be kebab-case)
                            const kebabCaseRegex = /^[a-z]+(-[a-z0-9]+)*$/;
                            const isKebabCase = kebabCaseRegex.test(comp.selector);
                            const hasAppPrefix = comp.selector.startsWith('app-') || 
                                                (comp.selector.includes('-') && comp.selector.split('-')[0].length <= 5);
                            
                            if (!isKebabCase) {
                                results.namingConventions.components.violations.push({
                                    component: comp.name,
                                    selector: comp.selector,
                                    violation: 'Selector should use kebab-case naming',
                                    severity: 'medium',
                                    styleGuideRule: 'Style 02-07',
                                    recommendation: 'Use kebab-case for component selectors (e.g., my-component)'
                                });
                            } else {
                                results.namingConventions.components.compliant++;
                            }
                            
                            if (!hasAppPrefix) {
                                results.namingConventions.components.violations.push({
                                    component: comp.name,
                                    selector: comp.selector,
                                    violation: 'Component should have a consistent prefix',
                                    severity: 'low',
                                    styleGuideRule: 'Style 02-08',
                                    recommendation: 'Add a consistent prefix to component selectors (e.g., app-my-component)'
                                });
                            }
                            
                            // Check class naming (should be PascalCase and end with Component)
                            const pascalCaseRegex = /^[A-Z][a-zA-Z0-9]*$/;
                            const isPascalCase = pascalCaseRegex.test(comp.name);
                            const endsWithComponent = comp.name.endsWith('Component');
                            
                            if (!isPascalCase) {
                                results.namingConventions.components.violations.push({
                                    component: comp.name,
                                    violation: 'Component class should use PascalCase naming',
                                    severity: 'medium',
                                    styleGuideRule: 'Style 02-03',
                                    recommendation: 'Use PascalCase for component class names'
                                });
                            }
                            
                            if (!endsWithComponent) {
                                results.namingConventions.components.violations.push({
                                    component: comp.name,
                                    violation: 'Component class should end with Component',
                                    severity: 'medium',
                                    styleGuideRule: 'Style 02-03',
                                    recommendation: 'Add Component suffix to component class names'
                                });
                            }
                        });
                        
                        // Check file naming patterns by analyzing script tags
                        const scripts = Array.from(document.querySelectorAll('script[src]'));
                        scripts.forEach(script => {
                            const src = script.getAttribute('src');
                            if (src && src.includes('component')) {
                                results.namingConventions.files.checked++;
                                
                                // Check for proper file naming (should be kebab-case.component.ts)
                                const fileNamingRegex = /[a-z-]+\\.component\\.(js|ts)/;
                                const hasProperNaming = fileNamingRegex.test(src);
                                if (hasProperNaming) {
                                    results.namingConventions.files.compliant++;
                                } else {
                                    results.namingConventions.files.violations.push({
                                        file: src,
                                        violation: 'File should follow kebab-case.component.ts naming pattern',
                                        severity: 'low',
                                        styleGuideRule: 'Style 02-02',
                                        recommendation: 'Use kebab-case.component.ts for component files'
                                    });
                                }
                            }
                        });
                    };

                    // Step 3: Analyze Component Architecture
                    const analyzeComponentArchitecture = () => {
                        let totalComponents = 0;
                        let complexComponents = 0;
                        
                        if (window.ng?.getComponent) {
                            try {
                                const allElements = Array.from(document.querySelectorAll('*'));
                                allElements.forEach(el => {
                                    try {
                                        const component = window.ng.getComponent(el);
                                        if (component && component.constructor) {
                                            totalComponents++;
                                            
                                            // Analyze component complexity (Style Guide: Keep components small)
                                            const propertyCount = Object.getOwnPropertyNames(component).length;
                                            const methodCount = Object.getOwnPropertyNames(component.constructor.prototype).length;
                                            
                                            if (propertyCount > 20 || methodCount > 15) {
                                                complexComponents++;
                                                results.componentArchitecture.singleResponsibility.violations.push({
                                                    component: component.constructor.name,
                                                    issue: 'Component may be too complex',
                                                    properties: propertyCount,
                                                    methods: methodCount,
                                                    severity: propertyCount > 30 ? 'high' : 'medium',
                                                    styleGuideRule: 'Style 05-15',
                                                    recommendation: 'Consider breaking down into smaller components or extracting services'
                                                });
                                            }
                                            
                                            // Check for proper lifecycle hook usage
                                            const hasOnInit = typeof component.ngOnInit === 'function';
                                            const hasOnDestroy = typeof component.ngOnDestroy === 'function';
                                            
                                            if (!hasOnInit && propertyCount > 10) {
                                                results.componentArchitecture.singleResponsibility.recommendations.push({
                                                    component: component.constructor.name,
                                                    recommendation: 'Consider implementing OnInit for initialization logic',
                                                    styleGuideRule: 'Style 09-01'
                                                });
                                            }
                                            
                                            if (!hasOnDestroy && propertyCount > 5) {
                                                results.componentArchitecture.singleResponsibility.recommendations.push({
                                                    component: component.constructor.name,
                                                    recommendation: 'Consider implementing OnDestroy for cleanup',
                                                    styleGuideRule: 'Style 09-01',
                                                    priority: 'high'
                                                });
                                            }
                                        }
                                    } catch (e) {
                                        // Skip elements without components
                                    }
                                });
                            } catch (e) {
                                console.warn('Could not analyze component architecture:', e);
                            }
                        }
                        
                        // Calculate scores
                        if (totalComponents > 0) {
                            results.componentArchitecture.singleResponsibility.score = 
                                Math.round(((totalComponents - complexComponents) / totalComponents) * 100);
                        }
                        
                        // Analyze function complexity in script content
                        const scripts = Array.from(document.querySelectorAll('script:not([src])'));
                        let longFunctions = 0;
                        let totalFunctions = 0;
                        
                        scripts.forEach(script => {
                            const content = script.textContent || '';
                            
                            // Simple regex to count function definitions
                            const functionRegex = /function\\s+\\w+\\s*\\([^)]*\\)\\s*\\{[^}]*\\}/g;
                            const arrowFunctionRegex = /\\w+\\s*=\\s*\\([^)]*\\)\\s*=>\\s*\\{[^}]*\\}/g;
                            
                            const functionMatches = content.match(functionRegex) || [];
                            const arrowFunctionMatches = content.match(arrowFunctionRegex) || [];
                            
                            [...functionMatches, ...arrowFunctionMatches].forEach(func => {
                                totalFunctions++;
                                const lines = func.split('\\n').length;
                                if (lines > 20) {
                                    longFunctions++;
                                    results.componentArchitecture.smallFunctions.violations.push({
                                        issue: 'Function exceeds recommended length',
                                        lines: lines,
                                        severity: lines > 50 ? 'high' : 'medium',
                                        styleGuideRule: 'Style 05-15',
                                        recommendation: 'Break down function into smaller functions (max 20 lines)'
                                    });
                                }
                            });
                        });
                        
                        if (totalFunctions > 0) {
                            results.componentArchitecture.smallFunctions.score = 
                                Math.round(((totalFunctions - longFunctions) / totalFunctions) * 100);
                        }
                    };

                    // Step 4: Analyze Code Organization
                    const analyzeCodeOrganization = () => {
                        // Check for feature module patterns
                        const featureSelector = 'script[src*=""feature""], script[src*=""module""]';
                        const hasFeatureModules = document.querySelectorAll(featureSelector).length > 0;
                        results.codeOrganization.featureModules.detected = hasFeatureModules;
                        
                        if (hasFeatureModules) {
                            results.codeOrganization.featureModules.score = 80;
                            results.codeOrganization.featureModules.recommendations.push({
                                recommendation: 'Good feature module organization detected',
                                type: 'positive'
                            });
                        } else {
                            results.codeOrganization.featureModules.score = 20;
                            results.codeOrganization.featureModules.recommendations.push({
                                recommendation: 'Consider organizing code into feature modules',
                                styleGuideRule: 'Style 04-09',
                                priority: 'medium'
                            });
                        }
                        
                        // Check for shared modules
                        const sharedSelector = 'script[src*=""shared""], script[src*=""common""]';
                        const hasSharedModules = document.querySelectorAll(sharedSelector).length > 0;
                        results.codeOrganization.sharedModules.detected = hasSharedModules;
                        
                        if (hasSharedModules) {
                            results.codeOrganization.sharedModules.score = 80;
                        } else {
                            results.codeOrganization.sharedModules.score = 30;
                            results.codeOrganization.sharedModules.recommendations.push({
                                recommendation: 'Consider creating shared modules for common functionality',
                                styleGuideRule: 'Style 04-10'
                            });
                        }
                        
                        // Analyze folder structure from script sources
                        const scripts = Array.from(document.querySelectorAll('script[src]'));
                        const paths = scripts.map(s => s.getAttribute('src')).filter(src => src && !src.startsWith('http'));
                        
                        const hasProperStructure = paths.some(path => 
                            path.includes('/components/') || 
                            path.includes('/services/') || 
                            path.includes('/modules/')
                        );
                        
                        if (hasProperStructure) {
                            results.codeOrganization.folderStructure.score = 80;
                        } else {
                            results.codeOrganization.folderStructure.score = 40;
                            results.codeOrganization.folderStructure.recommendations.push({
                                recommendation: 'Organize code into proper folder structure (components/, services/, modules/)',
                                styleGuideRule: 'Style 04-06'
                            });
                        }
                    };

                    // Step 5: Analyze Template and Styling
                    const analyzeTemplateAndStyling = () => {
                        // Check for Angular template syntax best practices
                        const elements = Array.from(document.querySelectorAll('*'));
                        let properTemplateUsage = 0;
                        let totalCheckableElements = 0;
                        
                        elements.forEach(el => {
                            // Check for proper event binding syntax
                            Array.from(el.attributes).forEach(attr => {
                                if (attr.name.startsWith('(') && attr.name.endsWith(')')) {
                                    totalCheckableElements++;
                                    // Good event binding syntax
                                    properTemplateUsage++;
                                } else if (attr.name.startsWith('on') && attr.value.includes('(')) {
                                    totalCheckableElements++;
                                    results.templateAndStyling.templateSyntax.violations.push({
                                        element: el.tagName.toLowerCase(),
                                        violation: 'Use Angular event binding syntax instead of DOM events',
                                        attribute: attr.name,
                                        severity: 'medium',
                                        styleGuideRule: 'Style 05-14',
                                        recommendation: 'Use (click) instead of onclick'
                                    });
                                }
                                
                                // Check for property binding
                                if (attr.name.startsWith('[') && attr.name.endsWith(']')) {
                                    totalCheckableElements++;
                                    properTemplateUsage++;
                                }
                            });
                        });
                        
                        if (totalCheckableElements > 0) {
                            results.templateAndStyling.templateSyntax.score = 
                                Math.round((properTemplateUsage / totalCheckableElements) * 100);
                        }
                        
                        // Check CSS styling patterns
                        const styleElements = Array.from(document.querySelectorAll('style'));
                        let goodCssPatterns = 0;
                        let totalCssRules = 0;
                        
                        styleElements.forEach(style => {
                            const css = style.textContent || '';
                            
                            // Check for component-scoped styles (presence of Angular generated selectors)
                            if (css.includes('_nghost') || css.includes('_ngcontent')) {
                                goodCssPatterns++;
                            }
                            
                            // Count CSS rules for scoring
                            const cssRuleRegex = /\\{[^}]*\\}/g;
                            const ruleCount = (css.match(cssRuleRegex) || []).length;
                            totalCssRules += ruleCount;
                        });
                        
                        if (totalCssRules > 0) {
                            results.templateAndStyling.cssStyling.score = 
                                Math.round((goodCssPatterns / styleElements.length) * 100);
                        }
                        
                        // Basic accessibility check
                        const images = document.querySelectorAll('img');
                        const imagesWithAlt = document.querySelectorAll('img[alt]');
                        const buttons = document.querySelectorAll('button');
                        const buttonsWithType = document.querySelectorAll('button[type]');
                        
                        let accessibilityScore = 100;
                        if (images.length > 0 && imagesWithAlt.length < images.length) {
                            accessibilityScore -= 20;
                            results.templateAndStyling.accessibility.violations.push({
                                violation: 'Images missing alt attributes',
                                count: images.length - imagesWithAlt.length,
                                severity: 'medium',
                                recommendation: 'Add alt attributes to all images for accessibility'
                            });
                        }
                        
                        if (buttons.length > 0 && buttonsWithType.length < buttons.length) {
                            accessibilityScore -= 15;
                            results.templateAndStyling.accessibility.violations.push({
                                violation: 'Buttons missing type attributes',
                                count: buttons.length - buttonsWithType.length,
                                severity: 'low',
                                recommendation: 'Add type attributes to buttons (button, submit, reset)'
                            });
                        }
                        
                        results.templateAndStyling.accessibility.score = Math.max(0, accessibilityScore);
                    };

                    // Step 6: Analyze Modern Angular Patterns
                    const analyzeModernAngularPatterns = () => {
                        // Check for standalone components
                        let standaloneCount = 0;
                        if (window.ng?.getComponent) {
                            try {
                                const allElements = Array.from(document.querySelectorAll('*'));
                                allElements.forEach(el => {
                                    try {
                                        const component = window.ng.getComponent(el);
                                        if (component && component.constructor) {
                                            const componentDef = component.constructor.ɵcmp;
                                            if (componentDef?.standalone === true) {
                                                standaloneCount++;
                                            }
                                        }
                                    } catch (e) {
                                        // Skip
                                    }
                                });
                            } catch (e) {
                                console.warn('Could not check standalone components:', e);
                            }
                        }
                        
                        results.modernAngularPatterns.standaloneComponents.usage = standaloneCount;
                        if (standaloneCount > 0) {
                            results.modernAngularPatterns.standaloneComponents.score = 90;
                            results.modernAngularPatterns.standaloneComponents.recommendations.push({
                                recommendation: 'Excellent: Using ' + standaloneCount + ' standalone components',
                                type: 'positive'
                            });
                        } else {
                            results.modernAngularPatterns.standaloneComponents.score = 30;
                            results.modernAngularPatterns.standaloneComponents.recommendations.push({
                                recommendation: 'Consider migrating to standalone components for better tree-shaking',
                                priority: 'medium'
                            });
                        }
                        
                        // Check for signals usage
                        const scripts = Array.from(document.querySelectorAll('script'));
                        const hasSignals = scripts.some(script => {
                            const content = script.textContent || '';
                            return content.includes('signal(') || content.includes('computed(') || content.includes('effect(');
                        });
                        
                        if (hasSignals) {
                            results.modernAngularPatterns.signals.usage = 1;
                            results.modernAngularPatterns.signals.score = 95;
                            results.modernAngularPatterns.signals.recommendations.push({
                                recommendation: 'Great: Using modern Angular signals',
                                type: 'positive'
                            });
                        } else {
                            results.modernAngularPatterns.signals.score = 40;
                            results.modernAngularPatterns.signals.recommendations.push({
                                recommendation: 'Consider using Angular signals for reactive state management',
                                priority: 'medium'
                            });
                        }
                        
                        // Check for new control flow (@if, @for)
                        const hasNewControlFlow = Array.from(document.querySelectorAll('*')).some(el => {
                            const content = el.innerHTML;
                            return content.includes('@if') || content.includes('@for') || content.includes('@switch');
                        });
                        
                        if (hasNewControlFlow) {
                            results.modernAngularPatterns.controlFlow.usage = 1;
                            results.modernAngularPatterns.controlFlow.score = 95;
                            results.modernAngularPatterns.controlFlow.recommendations.push({
                                recommendation: 'Excellent: Using new Angular 17+ control flow syntax',
                                type: 'positive'
                            });
                        } else {
                            results.modernAngularPatterns.controlFlow.score = 50;
                            results.modernAngularPatterns.controlFlow.recommendations.push({
                                recommendation: 'Consider using new @if, @for control flow syntax (Angular 17+)',
                                priority: 'low'
                            });
                        }
                    };

                    // Step 7: Calculate Overall Compliance Score
                    const calculateOverallScore = () => {
                        const scores = [
                            results.componentArchitecture.singleResponsibility.score,
                            results.componentArchitecture.smallFunctions.score,
                            results.codeOrganization.featureModules.score,
                            results.codeOrganization.sharedModules.score,
                            results.codeOrganization.folderStructure.score,
                            results.templateAndStyling.templateSyntax.score,
                            results.templateAndStyling.cssStyling.score,
                            results.templateAndStyling.accessibility.score,
                            results.modernAngularPatterns.standaloneComponents.score,
                            results.modernAngularPatterns.signals.score,
                            results.modernAngularPatterns.controlFlow.score
                        ];
                        
                        const validScores = scores.filter(score => score > 0);
                        if (validScores.length > 0) {
                            results.complianceOverview.overallComplianceScore = 
                                Math.round(validScores.reduce((a, b) => a + b, 0) / validScores.length);
                        }
                        
                        // Count violations and recommendations
                        results.complianceOverview.totalChecks = validScores.length;
                        results.complianceOverview.passedChecks = validScores.filter(score => score >= 80).length;
                        results.complianceOverview.failedChecks = validScores.filter(score => score < 60).length;
                        results.complianceOverview.warningChecks = validScores.filter(score => score >= 60 && score < 80).length;
                        
                        // Assign compliance grade
                        const overallScore = results.complianceOverview.overallComplianceScore;
                        if (overallScore >= 90) results.complianceOverview.complianceGrade = 'A';
                        else if (overallScore >= 80) results.complianceOverview.complianceGrade = 'B';
                        else if (overallScore >= 70) results.complianceOverview.complianceGrade = 'C';
                        else if (overallScore >= 60) results.complianceOverview.complianceGrade = 'D';
                        else results.complianceOverview.complianceGrade = 'F';
                    };

                    // Step 8: Generate Detailed Recommendations
                    const generateRecommendations = () => {
                        const recommendations = [];
                        
                        // High priority recommendations
                        if (results.componentArchitecture.singleResponsibility.score < 70) {
                            recommendations.push({
                                priority: 'high',
                                category: 'Component Architecture',
                                title: 'Reduce Component Complexity',
                                description: 'Components are too complex and violate single responsibility principle',
                                actionItems: [
                                    'Break down large components into smaller, focused components',
                                    'Extract business logic into services',
                                    'Use composition over inheritance',
                                    'Implement proper lifecycle hooks'
                                ],
                                styleGuideRule: 'Style 05-15',
                                estimatedEffort: 'Medium'
                            });
                        }
                        
                        if (results.templateAndStyling.accessibility.score < 80) {
                            recommendations.push({
                                priority: 'high',
                                category: 'Accessibility',
                                title: 'Improve Accessibility Compliance',
                                description: 'Application has accessibility issues that need attention',
                                actionItems: [
                                    'Add alt attributes to all images',
                                    'Ensure proper button types',
                                    'Add ARIA labels where needed',
                                    'Test with screen readers'
                                ],
                                estimatedEffort: 'Low'
                            });
                        }
                        
                        // Medium priority recommendations
                        if (results.modernAngularPatterns.standaloneComponents.score < 60) {
                            recommendations.push({
                                priority: 'medium',
                                category: 'Modern Angular',
                                title: 'Migrate to Standalone Components',
                                description: 'Consider adopting standalone components for better tree-shaking',
                                actionItems: [
                                    'Identify components suitable for standalone migration',
                                    'Add standalone: true to component decorators',
                                    'Move module imports to component imports',
                                    'Update routing configuration'
                                ],
                                estimatedEffort: 'Medium'
                            });
                        }
                        
                        if (!results.codeOrganization.featureModules.detected) {
                            recommendations.push({
                                priority: 'medium',
                                category: 'Code Organization',
                                title: 'Implement Feature Modules',
                                description: 'Organize code into feature modules for better maintainability',
                                actionItems: [
                                    'Identify feature boundaries',
                                    'Create feature modules',
                                    'Implement lazy loading',
                                    'Organize shared functionality'
                                ],
                                styleGuideRule: 'Style 04-09',
                                estimatedEffort: 'High'
                            });
                        }
                        
                        // Low priority recommendations
                        if (results.modernAngularPatterns.signals.score < 60) {
                            recommendations.push({
                                priority: 'low',
                                category: 'Modern Angular',
                                title: 'Adopt Angular Signals',
                                description: 'Consider using signals for reactive state management',
                                actionItems: [
                                    'Learn about Angular signals',
                                    'Identify state that could benefit from signals',
                                    'Gradually migrate from traditional reactive patterns',
                                    'Use computed and effect where appropriate'
                                ],
                                estimatedEffort: 'Medium'
                            });
                        }
                        
                        results.detailedRecommendations = recommendations;
                    };

                    // Step 9: Create Action Plan
                    const createActionPlan = () => {
                        const actionPlan = [];
                        
                        // Immediate actions (Week 1-2)
                        actionPlan.push({
                            phase: 'Immediate (Week 1-2)',
                            actions: [
                                'Fix accessibility violations (add alt attributes, button types)',
                                'Address naming convention violations',
                                'Review and fix template syntax issues'
                            ],
                            impact: 'High',
                            effort: 'Low'
                        });
                        
                        // Short-term actions (Week 3-6)
                        actionPlan.push({
                            phase: 'Short-term (Week 3-6)',
                            actions: [
                                'Refactor overly complex components',
                                'Implement proper component lifecycle hooks',
                                'Organize code into proper folder structure',
                                'Create shared modules for common functionality'
                            ],
                            impact: 'High',
                            effort: 'Medium'
                        });
                        
                        // Medium-term actions (Month 2-3)
                        actionPlan.push({
                            phase: 'Medium-term (Month 2-3)',
                            actions: [
                                'Implement feature modules and lazy loading',
                                'Begin migration to standalone components',
                                'Adopt modern Angular patterns (signals, new control flow)',
                                'Improve service architecture and dependency injection'
                            ],
                            impact: 'Medium',
                            effort: 'High'
                        });
                        
                        // Long-term actions (Month 4+)
                        actionPlan.push({
                            phase: 'Long-term (Month 4+)',
                            actions: [
                                'Complete standalone component migration',
                                'Implement comprehensive testing strategy',
                                'Performance optimization and monitoring',
                                'Continuous compliance monitoring'
                            ],
                            impact: 'Medium',
                            effort: 'Medium'
                        });
                        
                        results.complianceActionPlan = actionPlan;
                    };

                    // Execute analysis workflow
                    try {
                        if (!detectAngular()) {
                            return 'Angular application not detected on this page';
                        }
                        
                        analyzeNamingConventions();
                        analyzeComponentArchitecture();
                        analyzeCodeOrganization();
                        analyzeTemplateAndStyling();
                        analyzeModernAngularPatterns();
                        calculateOverallScore();
                        generateRecommendations();
                        createActionPlan();
                        
                        return results;
                        
                    } catch (error) {
                        return {
                            error: 'Analysis failed',
                            message: error.message,
                            angularDetected: results.complianceOverview.angularDetected
                        };
                    }
                })();
            ";

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to validate Angular Style Guide compliance: {ex.Message}";
        }
    }
}
