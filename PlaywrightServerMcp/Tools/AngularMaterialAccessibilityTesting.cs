using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Playwright.Core.Services;

namespace PlaywrightServerMcp.Tools;

/// <summary>
/// Angular Material Accessibility Testing - Comprehensive Material Design accessibility compliance validation
/// ANG-020: Angular Material Accessibility Testing Implementation
/// </summary>
[McpServerToolType]
public class AngularMaterialAccessibilityTesting(PlaywrightSessionManager sessionManager)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool]
    [Description("Validate Angular Material components for accessibility compliance with comprehensive WCAG testing. See skills/playwright-mcp/tools/angular/material-accessibility-testing.md.")]
    public async Task<string> ValidateMaterialAccessibilityCompliance(
        string sessionId = "default",
        string componentSelector = "",
        string wcagLevel = "AA",
        bool includeDetails = true,
        bool testColorContrast = true,
        bool testKeyboardNavigation = true,
        bool testScreenReader = true,
        bool generateRecommendations = true)
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = """

                                         (() => {
                                             const results = {
                                                 testSummary: {
                                                     angularDetected: false,
                                                     angularVersion: null,
                                                     materialDetected: false,
                                                     materialVersion: null,
                                                     cdkDetected: false,
                                                     testTimestamp: new Date().toISOString(),
                                                     wcagLevel: '
                         """ + wcagLevel + """
                                           ',
                                                                       overallComplianceScore: 0,
                                                                       totalViolations: 0,
                                                                       criticalViolations: 0,
                                                                       testStatus: 'not_started'
                                                                   },
                                                                   materialComponentsAnalysis: {
                                                                       componentsFound: [],
                                                                       componentTypes: {},
                                                                       totalComponents: 0,
                                                                       accessibleComponents: 0,
                                                                       componentsWithIssues: 0
                                                                   },
                                                                   accessibilityViolations: {
                                                                       critical: [],
                                                                       major: [],
                                                                       minor: [],
                                                                       summary: {
                                                                           perceivable: { passed: 0, failed: 0, warnings: 0 },
                                                                           operable: { passed: 0, failed: 0, warnings: 0 },
                                                                           understandable: { passed: 0, failed: 0, warnings: 0 },
                                                                           robust: { passed: 0, failed: 0, warnings: 0 }
                                                                       }
                                                                   },
                                                                   colorContrastAnalysis: {
                                                                       tested: 
                                           """ + testColorContrast.ToString().ToLower() + """
                ,
                                            violations: [],
                                            passedElements: [],
                                            averageContrastRatio: 0,
                                            minimumContrastMet: false
                                        },
                                        keyboardNavigationAnalysis: {
                                            tested: 
                """ + testKeyboardNavigation.ToString().ToLower() + """
                                                                    ,
                                                                                                focusableElements: [],
                                                                                                tabOrder: [],
                                                                                                keyboardTraps: [],
                                                                                                missingFocusIndicators: [],
                                                                                                navigationScore: 0
                                                                                            },
                                                                                            screenReaderAnalysis: {
                                                                                                tested: 
                                                                    """ + testScreenReader.ToString().ToLower() + """
                ,
                                            ariaLabels: [],
                                            missingLabels: [],
                                            incorrectRoles: [],
                                            semanticStructure: {},
                                            screenReaderScore: 0
                                        },
                                        materialSpecificTests: {
                                            buttonAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            formControlAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            navigationAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            dialogAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            tableAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            menuAccessibility: { tested: false, passed: 0, failed: 0, issues: [] }
                                        },
                                        recommendations: {
                                            immediate: [],
                                            shortTerm: [],
                                            longTerm: [],
                                            bestPractices: []
                                        },
                                        complianceReport: {
                                            wcagPrinciples: {},
                                            materialDesignCompliance: {},
                                            actionPlan: []
                                        }
                                    };

                                    // Step 1: Angular and Material Detection
                                    const detectFrameworks = () => {
                                        // Angular Detection
                                        const hasAngularGlobal = !!(window.ng || window.ngDevMode || window.getAllAngularRootElements);
                                        const hasNgElements = !!document.querySelector('[ng-version]');
                                        const hasAngularComponents = !!document.querySelector('*[ng-reflect-router-outlet], *[_nghost], *[_ngcontent]');
                                        
                                        results.testSummary.angularDetected = hasAngularGlobal || hasNgElements || hasAngularComponents;
                                        
                                        if (results.testSummary.angularDetected) {
                                            const versionElement = document.querySelector('[ng-version]');
                                            if (versionElement) {
                                                results.testSummary.angularVersion = versionElement.getAttribute('ng-version');
                                            }
                                        }
                                        
                                        // Material Detection
                                        const materialSelectors = [
                                            'mat-button', 'mat-icon-button', 'mat-fab', 'mat-mini-fab',
                                            'mat-card', 'mat-dialog-container', 'mat-form-field',
                                            'mat-input', 'mat-select', 'mat-checkbox', 'mat-radio-button',
                                            'mat-slide-toggle', 'mat-slider', 'mat-progress-bar',
                                            'mat-progress-spinner', 'mat-table', 'mat-paginator',
                                            'mat-sort', 'mat-menu', 'mat-toolbar', 'mat-sidenav',
                                            'mat-tab-group', 'mat-expansion-panel', 'mat-list',
                                            'mat-grid-list', 'mat-chip-list', 'mat-autocomplete'
                                        ];
                                        
                                        const materialElements = materialSelectors.map(selector => 
                                            Array.from(document.querySelectorAll(selector))
                                        ).flat();
                                        
                                        results.testSummary.materialDetected = materialElements.length > 0;
                                        
                                        // CDK Detection
                                        const cdkElements = document.querySelectorAll('[cdk-overlay-origin], [cdkDrag], [cdkDropList], [cdkPortalOutlet]');
                                        results.testSummary.cdkDetected = cdkElements.length > 0;
                                        
                                        // Try to detect Material version
                                        if (results.testSummary.materialDetected) {
                                            // Check for Material theme variables or classes
                                            const hasMatTheme = document.querySelector('.mat-theme') || 
                                                              document.querySelector('.mat-app-background') ||
                                                              getComputedStyle(document.body).getPropertyValue('--mat-toolbar-container-background-color');
                                            
                                            if (hasMatTheme) {
                                                results.testSummary.materialVersion = 'Material 15+';
                                            } else {
                                                results.testSummary.materialVersion = 'Material (version unknown)';
                                            }
                                        }
                                        
                                        return results.testSummary.materialDetected;
                                    };

                                    // Step 2: Material Components Analysis
                                    const analyzeMaterialComponents = () => {
                                        const componentTypes = {};
                                        const componentsFound = [];
                                        
                                        const materialComponentMap = {
                                            'mat-button': 'Button',
                                            'mat-icon-button': 'Icon Button',
                                            'mat-fab': 'Floating Action Button',
                                            'mat-mini-fab': 'Mini FAB',
                                            'mat-card': 'Card',
                                            'mat-form-field': 'Form Field',
                                            'mat-input': 'Input',
                                            'mat-select': 'Select',
                                            'mat-checkbox': 'Checkbox',
                                            'mat-radio-button': 'Radio Button',
                                            'mat-slide-toggle': 'Slide Toggle',
                                            'mat-slider': 'Slider',
                                            'mat-table': 'Table',
                                            'mat-menu': 'Menu',
                                            'mat-toolbar': 'Toolbar',
                                            'mat-sidenav': 'Side Navigation',
                                            'mat-tab-group': 'Tabs',
                                            'mat-list': 'List',
                                            'mat-dialog-container': 'Dialog'
                                        };
                                        
                                        Object.entries(materialComponentMap).forEach(([selector, name]) => {
                                            const elements = document.querySelectorAll(selector);
                                            if (elements.length > 0) {
                                                componentTypes[name] = elements.length;
                                                
                                                elements.forEach((element, index) => {
                                                    componentsFound.push({
                                                        type: name,
                                                        selector: selector,
                                                        index: index,
                                                        element: element.tagName.toLowerCase(),
                                                        hasAriaLabel: !!element.getAttribute('aria-label'),
                                                        hasAriaLabelledBy: !!element.getAttribute('aria-labelledby'),
                                                        hasRole: !!element.getAttribute('role'),
                                                        isDisabled: element.hasAttribute('disabled') || element.getAttribute('aria-disabled') === 'true',
                                                        isVisible: element.offsetParent !== null,
                                                        tabIndex: element.tabIndex
                                                    });
                                                });
                                            }
                                        });
                                        
                                        results.materialComponentsAnalysis = {
                                            componentsFound: componentsFound,
                                            componentTypes: componentTypes,
                                            totalComponents: componentsFound.length,
                                            accessibleComponents: 0, // Will be calculated later
                                            componentsWithIssues: 0 // Will be calculated later
                                        };
                                    };

                                    // Step 3: Color Contrast Analysis
                                    const analyzeColorContrast = () => {
                                        if (!results.colorContrastAnalysis.tested) return;
                                        
                                        const violations = [];
                                        const passedElements = [];
                                        let totalContrast = 0;
                                        let elementCount = 0;
                                        
                                        // Helper function to calculate luminance
                                        const getLuminance = (r, g, b) => {
                                            const [rs, gs, bs] = [r, g, b].map(c => {
                                                c = c / 255;
                                                return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
                                            });
                                            return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
                                        };
                                        
                                        // Helper function to calculate contrast ratio
                                        const getContrastRatio = (color1, color2) => {
                                            const lum1 = getLuminance(color1.r, color1.g, color1.b);
                                            const lum2 = getLuminance(color2.r, color2.g, color2.b);
                                            const brightest = Math.max(lum1, lum2);
                                            const darkest = Math.min(lum1, lum2);
                                            return (brightest + 0.05) / (darkest + 0.05);
                                        };
                                        
                                        // Helper function to parse RGB color
                                        const parseColor = (colorStr) => {
                                            const canvas = document.createElement('canvas');
                                            canvas.width = canvas.height = 1;
                                            const ctx = canvas.getContext('2d');
                                            ctx.fillStyle = colorStr;
                                            ctx.fillRect(0, 0, 1, 1);
                                            const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
                                            return { r, g, b };
                                        };
                                        
                                        // Test Material components for contrast
                                        results.materialComponentsAnalysis.componentsFound.forEach(comp => {
                                            try {
                                                const elements = document.querySelectorAll(comp.selector);
                                                elements.forEach(element => {
                                                    const style = window.getComputedStyle(element);
                                                    const textColor = parseColor(style.color);
                                                    const bgColor = parseColor(style.backgroundColor);
                                                    
                                                    // If background is transparent, try to find parent background
                                                    let actualBgColor = bgColor;
                                                    if (style.backgroundColor === 'rgba(0, 0, 0, 0)' || style.backgroundColor === 'transparent') {
                                                        let parent = element.parentElement;
                                                        while (parent && (window.getComputedStyle(parent).backgroundColor === 'rgba(0, 0, 0, 0)' || 
                                                               window.getComputedStyle(parent).backgroundColor === 'transparent')) {
                                                            parent = parent.parentElement;
                                                        }
                                                        if (parent) {
                                                            actualBgColor = parseColor(window.getComputedStyle(parent).backgroundColor);
                                                        } else {
                                                            actualBgColor = { r: 255, g: 255, b: 255 }; // Default to white
                                                        }
                                                    }
                                                    
                                                    const contrastRatio = getContrastRatio(textColor, actualBgColor);
                                                    totalContrast += contrastRatio;
                                                    elementCount++;
                                                    
                                                    const fontSize = parseFloat(style.fontSize);
                                                    const isBold = style.fontWeight === 'bold' || parseInt(style.fontWeight) >= 700;
                                                    const isLargeText = fontSize >= 18 || (fontSize >= 14 && isBold);
                                                    
                                                    const requiredRatio = isLargeText ? 3.0 : 4.5; // WCAG AA standards
                                                    const aaaRequiredRatio = isLargeText ? 4.5 : 7.0; // WCAG AAA standards
                                                    
                                                    const testResult = {
                                                        element: comp.type,
                                                        selector: comp.selector,
                                                        contrastRatio: Math.round(contrastRatio * 100) / 100,
                                                        requiredRatio: requiredRatio,
                                                        aaaRequiredRatio: aaaRequiredRatio,
                                                        isLargeText: isLargeText,
                                                        wcagAA: contrastRatio >= requiredRatio,
                                                        wcagAAA: contrastRatio >= aaaRequiredRatio,
                                                        textColor: style.color,
                                                        backgroundColor: style.backgroundColor
                                                    };
                                                    
                                                    if (contrastRatio >= requiredRatio) {
                                                        passedElements.push(testResult);
                                                    } else {
                                                        violations.push({
                                                            ...testResult,
                                                            severity: contrastRatio < 3.0 ? 'critical' : 'major',
                                                            recommendation: `Increase contrast ratio to at least ${requiredRatio}:1 for WCAG AA compliance`
                                                        });
                                                    }
                                                });
                                            } catch (error) {
                                                console.warn('Color contrast analysis failed for', comp.type, error);
                                            }
                                        });
                                        
                                        results.colorContrastAnalysis = {
                                            tested: true,
                                            violations: violations,
                                            passedElements: passedElements,
                                            averageContrastRatio: elementCount > 0 ? Math.round((totalContrast / elementCount) * 100) / 100 : 0,
                                            minimumContrastMet: violations.length === 0
                                        };
                                    };

                                    // Step 4: Keyboard Navigation Analysis
                                    const analyzeKeyboardNavigation = () => {
                                        if (!results.keyboardNavigationAnalysis.tested) return;
                                        
                                        const focusableElements = [];
                                        const tabOrder = [];
                                        const missingFocusIndicators = [];
                                        const keyboardTraps = [];
                                        
                                        // Find all focusable Material elements
                                        const focusableSelectors = [
                                            'mat-button', 'mat-icon-button', 'mat-fab', 'mat-mini-fab',
                                            'mat-form-field input', 'mat-form-field textarea', 'mat-select',
                                            'mat-checkbox', 'mat-radio-button', 'mat-slide-toggle',
                                            'mat-slider', 'mat-menu-item', 'mat-tab', 'mat-list-option'
                                        ];
                                        
                                        focusableSelectors.forEach(selector => {
                                            const elements = document.querySelectorAll(selector);
                                            elements.forEach((element, index) => {
                                                if (element.offsetParent !== null && !element.hasAttribute('disabled')) {
                                                    const focusableElement = {
                                                        type: selector,
                                                        index: index,
                                                        tabIndex: element.tabIndex,
                                                        hasVisibleFocus: false,
                                                        isKeyboardAccessible: true,
                                                        ariaLabel: element.getAttribute('aria-label'),
                                                        role: element.getAttribute('role')
                                                    };
                                                    
                                                    // Check if element has visible focus indicators
                                                    const style = window.getComputedStyle(element, ':focus');
                                                    const hasOutline = style.outline !== 'none' && style.outline !== '0px';
                                                    const hasBoxShadow = style.boxShadow !== 'none';
                                                    const hasBorder = style.borderWidth !== '0px';
                                                    
                                                    focusableElement.hasVisibleFocus = hasOutline || hasBoxShadow || hasBorder;
                                                    
                                                    if (!focusableElement.hasVisibleFocus) {
                                                        missingFocusIndicators.push({
                                                            element: selector,
                                                            index: index,
                                                            recommendation: 'Add visible focus indicators using :focus pseudo-class'
                                                        });
                                                    }
                                                    
                                                    focusableElements.push(focusableElement);
                                                    
                                                    // Build tab order
                                                    if (element.tabIndex >= 0) {
                                                        tabOrder.push({
                                                            element: selector,
                                                            tabIndex: element.tabIndex,
                                                            order: element.tabIndex === 0 ? 999 : element.tabIndex
                                                        });
                                                    }
                                                }
                                            });
                                        });
                                        
                                        // Sort tab order
                                        tabOrder.sort((a, b) => a.order - b.order);
                                        
                                        // Calculate navigation score
                                        const totalFocusable = focusableElements.length;
                                        const withGoodFocus = focusableElements.filter(el => el.hasVisibleFocus).length;
                                        const navigationScore = totalFocusable > 0 ? Math.round((withGoodFocus / totalFocusable) * 100) : 100;
                                        
                                        results.keyboardNavigationAnalysis = {
                                            tested: true,
                                            focusableElements: focusableElements,
                                            tabOrder: tabOrder,
                                            keyboardTraps: keyboardTraps,
                                            missingFocusIndicators: missingFocusIndicators,
                                            navigationScore: navigationScore
                                        };
                                    };

                                    // Step 5: Screen Reader Analysis
                                    const analyzeScreenReader = () => {
                                        if (!results.screenReaderAnalysis.tested) return;
                                        
                                        const ariaLabels = [];
                                        const missingLabels = [];
                                        const incorrectRoles = [];
                                        const semanticStructure = {
                                            headings: 0,
                                            landmarks: 0,
                                            lists: 0,
                                            tables: 0,
                                            forms: 0
                                        };
                                        
                                        // Analyze Material components for screen reader compatibility
                                        results.materialComponentsAnalysis.componentsFound.forEach(comp => {
                                            const elements = document.querySelectorAll(comp.selector);
                                            elements.forEach((element, index) => {
                                                const ariaLabel = element.getAttribute('aria-label');
                                                const ariaLabelledBy = element.getAttribute('aria-labelledby');
                                                const ariaDescribedBy = element.getAttribute('aria-describedby');
                                                const role = element.getAttribute('role');
                                                
                                                if (ariaLabel || ariaLabelledBy) {
                                                    ariaLabels.push({
                                                        element: comp.type,
                                                        index: index,
                                                        ariaLabel: ariaLabel,
                                                        ariaLabelledBy: ariaLabelledBy,
                                                        ariaDescribedBy: ariaDescribedBy,
                                                        role: role
                                                    });
                                                } else {
                                                    // Check if element needs a label
                                                    const needsLabel = ['mat-button', 'mat-icon-button', 'mat-fab', 'mat-mini-fab', 
                                                                     'mat-checkbox', 'mat-radio-button', 'mat-slide-toggle'].includes(comp.selector);
                                                    
                                                    if (needsLabel && !element.textContent.trim()) {
                                                        missingLabels.push({
                                                            element: comp.type,
                                                            index: index,
                                                            recommendation: 'Add aria-label or aria-labelledby attribute',
                                                            severity: 'major'
                                                        });
                                                    }
                                                }
                                                
                                                // Verify role appropriateness
                                                const expectedRoles = {
                                                    'mat-button': ['button'],
                                                    'mat-checkbox': ['checkbox'],
                                                    'mat-radio-button': ['radio'],
                                                    'mat-slider': ['slider'],
                                                    'mat-menu': ['menu'],
                                                    'mat-table': ['table', 'grid']
                                                };
                                                
                                                const expectedRole = expectedRoles[comp.selector];
                                                if (expectedRole && role && !expectedRole.includes(role)) {
                                                    incorrectRoles.push({
                                                        element: comp.type,
                                                        index: index,
                                                        currentRole: role,
                                                        expectedRole: expectedRole,
                                                        severity: 'major'
                                                    });
                                                }
                                            });
                                        });
                                        
                                        // Analyze semantic structure
                                        semanticStructure.headings = document.querySelectorAll('h1, h2, h3, h4, h5, h6').length;
                                        semanticStructure.landmarks = document.querySelectorAll('[role=banner], [role=navigation], [role=main], [role=complementary], [role=contentinfo], header, nav, main, aside, footer').length;
                                        semanticStructure.lists = document.querySelectorAll('ul, ol, mat-list').length;
                                        semanticStructure.tables = document.querySelectorAll('table, mat-table').length;
                                        semanticStructure.forms = document.querySelectorAll('form, mat-form-field').length;
                                        
                                        // Calculate screen reader score
                                        const totalComponents = results.materialComponentsAnalysis.totalComponents;
                                        const withLabels = ariaLabels.length;
                                        const missing = missingLabels.length;
                                        const screenReaderScore = totalComponents > 0 ? Math.round(((withLabels) / totalComponents) * 100) : 100;
                                        
                                        results.screenReaderAnalysis = {
                                            tested: true,
                                            ariaLabels: ariaLabels,
                                            missingLabels: missingLabels,
                                            incorrectRoles: incorrectRoles,
                                            semanticStructure: semanticStructure,
                                            screenReaderScore: screenReaderScore
                                        };
                                    };

                                    // Step 6: Material-Specific Accessibility Tests
                                    const testMaterialSpecificAccessibility = () => {
                                        const materialTests = {
                                            buttonAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            formControlAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            navigationAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            dialogAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            tableAccessibility: { tested: false, passed: 0, failed: 0, issues: [] },
                                            menuAccessibility: { tested: false, passed: 0, failed: 0, issues: [] }
                                        };
                                        
                                        // Test Button Accessibility
                                        const buttons = document.querySelectorAll('mat-button, mat-icon-button, mat-fab, mat-mini-fab');
                                        if (buttons.length > 0) {
                                            materialTests.buttonAccessibility.tested = true;
                                            buttons.forEach((button, index) => {
                                                const issues = [];
                                                
                                                // Check for accessible name
                                                const hasAccessibleName = button.getAttribute('aria-label') || 
                                                                        button.getAttribute('aria-labelledby') || 
                                                                        button.textContent.trim();
                                                if (!hasAccessibleName) {
                                                    issues.push('Missing accessible name (aria-label or text content)');
                                                }
                                                
                                                // Check if disabled buttons are properly marked
                                                if (button.hasAttribute('disabled') && button.getAttribute('aria-disabled') !== 'true') {
                                                    issues.push('Disabled button should have aria-disabled=true');
                                                }
                                                
                                                // Check focus management
                                                if (button.tabIndex < 0 && !button.hasAttribute('disabled')) {
                                                    issues.push('Button should be focusable (tabindex >= 0)');
                                                }
                                                
                                                if (issues.length === 0) {
                                                    materialTests.buttonAccessibility.passed++;
                                                } else {
                                                    materialTests.buttonAccessibility.failed++;
                                                    materialTests.buttonAccessibility.issues.push({
                                                        element: `Button ${index + 1}`,
                                                        issues: issues
                                                    });
                                                }
                                            });
                                        }
                                        
                                        // Test Form Control Accessibility
                                        const formControls = document.querySelectorAll('mat-form-field, mat-checkbox, mat-radio-button, mat-slide-toggle');
                                        if (formControls.length > 0) {
                                            materialTests.formControlAccessibility.tested = true;
                                            formControls.forEach((control, index) => {
                                                const issues = [];
                                                
                                                // Check for proper labeling
                                                const input = control.querySelector('input, textarea, mat-select');
                                                if (input) {
                                                    const hasLabel = control.querySelector('mat-label') || 
                                                                   input.getAttribute('aria-label') || 
                                                                   input.getAttribute('aria-labelledby') ||
                                                                   input.getAttribute('placeholder');
                                                    if (!hasLabel) {
                                                        issues.push('Form control missing label');
                                                    }
                                                    
                                                    // Check for error announcement
                                                    const hasError = control.querySelector('mat-error');
                                                    if (hasError && !input.getAttribute('aria-describedby')) {
                                                        issues.push('Error messages should be associated with aria-describedby');
                                                    }
                                                }
                                                
                                                if (issues.length === 0) {
                                                    materialTests.formControlAccessibility.passed++;
                                                } else {
                                                    materialTests.formControlAccessibility.failed++;
                                                    materialTests.formControlAccessibility.issues.push({
                                                        element: `Form Control ${index + 1}`,
                                                        issues: issues
                                                    });
                                                }
                                            });
                                        }
                                        
                                        // Test Navigation Accessibility
                                        const navElements = document.querySelectorAll('mat-toolbar, mat-sidenav, mat-tab-group');
                                        if (navElements.length > 0) {
                                            materialTests.navigationAccessibility.tested = true;
                                            navElements.forEach((nav, index) => {
                                                const issues = [];
                                                
                                                if (nav.tagName.toLowerCase() === 'mat-toolbar') {
                                                    if (!nav.getAttribute('role') || nav.getAttribute('role') !== 'banner') {
                                                        issues.push('Toolbar should have role=banner or be wrapped in header element');
                                                    }
                                                }
                                                
                                                if (nav.tagName.toLowerCase() === 'mat-tab-group') {
                                                    const tabs = nav.querySelectorAll('mat-tab');
                                                    tabs.forEach(tab => {
                                                        if (!tab.getAttribute('aria-label') && !tab.textContent.trim()) {
                                                            issues.push('Tab missing accessible name');
                                                        }
                                                    });
                                                }
                                                
                                                if (issues.length === 0) {
                                                    materialTests.navigationAccessibility.passed++;
                                                } else {
                                                    materialTests.navigationAccessibility.failed++;
                                                    materialTests.navigationAccessibility.issues.push({
                                                        element: `Navigation ${index + 1}`,
                                                        issues: issues
                                                    });
                                                }
                                            });
                                        }
                                        
                                        // Test Dialog Accessibility
                                        const dialogs = document.querySelectorAll('mat-dialog-container');
                                        if (dialogs.length > 0) {
                                            materialTests.dialogAccessibility.tested = true;
                                            dialogs.forEach((dialog, index) => {
                                                const issues = [];
                                                
                                                if (!dialog.getAttribute('role') || dialog.getAttribute('role') !== 'dialog') {
                                                    issues.push('Dialog should have role=dialog');
                                                }
                                                
                                                if (!dialog.getAttribute('aria-labelledby') && !dialog.getAttribute('aria-label')) {
                                                    issues.push('Dialog should have aria-labelledby or aria-label');
                                                }
                                                
                                                if (issues.length === 0) {
                                                    materialTests.dialogAccessibility.passed++;
                                                } else {
                                                    materialTests.dialogAccessibility.failed++;
                                                    materialTests.dialogAccessibility.issues.push({
                                                        element: `Dialog ${index + 1}`,
                                                        issues: issues
                                                    });
                                                }
                                            });
                                        }
                                        
                                        // Test Table Accessibility
                                        const tables = document.querySelectorAll('mat-table');
                                        if (tables.length > 0) {
                                            materialTests.tableAccessibility.tested = true;
                                            tables.forEach((table, index) => {
                                                const issues = [];
                                                
                                                const headers = table.querySelectorAll('mat-header-cell');
                                                if (headers.length === 0) {
                                                    issues.push('Table missing header cells');
                                                }
                                                
                                                if (!table.getAttribute('role') || table.getAttribute('role') !== 'table') {
                                                    issues.push('Table should have role=table');
                                                }
                                                
                                                if (issues.length === 0) {
                                                    materialTests.tableAccessibility.passed++;
                                                } else {
                                                    materialTests.tableAccessibility.failed++;
                                                    materialTests.tableAccessibility.issues.push({
                                                        element: `Table ${index + 1}`,
                                                        issues: issues
                                                    });
                                                }
                                            });
                                        }
                                        
                                        // Test Menu Accessibility
                                        const menus = document.querySelectorAll('mat-menu');
                                        if (menus.length > 0) {
                                            materialTests.menuAccessibility.tested = true;
                                            menus.forEach((menu, index) => {
                                                const issues = [];
                                                
                                                if (!menu.getAttribute('role') || menu.getAttribute('role') !== 'menu') {
                                                    issues.push('Menu should have role=menu');
                                                }
                                                
                                                const menuItems = menu.querySelectorAll('mat-menu-item');
                                                menuItems.forEach(item => {
                                                    if (!item.getAttribute('role') || item.getAttribute('role') !== 'menuitem') {
                                                        issues.push('Menu items should have role=menuitem');
                                                    }
                                                });
                                                
                                                if (issues.length === 0) {
                                                    materialTests.menuAccessibility.passed++;
                                                } else {
                                                    materialTests.menuAccessibility.failed++;
                                                    materialTests.menuAccessibility.issues.push({
                                                        element: `Menu ${index + 1}`,
                                                        issues: issues
                                                    });
                                                }
                                            });
                                        }
                                        
                                        results.materialSpecificTests = materialTests;
                                    };

                                    // Step 7: Generate Accessibility Violations Summary
                                    const generateViolationsSummary = () => {
                                        const violations = {
                                            critical: [],
                                            major: [],
                                            minor: []
                                        };
                                        
                                        // Color contrast violations
                                        results.colorContrastAnalysis.violations.forEach(violation => {
                                            violations[violation.severity].push({
                                                type: 'Color Contrast',
                                                element: violation.element,
                                                description: `Contrast ratio ${violation.contrastRatio}:1 does not meet WCAG ${results.testSummary.wcagLevel} requirements (${violation.requiredRatio}:1)`,
                                                recommendation: violation.recommendation,
                                                wcagPrinciple: 'Perceivable'
                                            });
                                        });
                                        
                                        // Screen reader violations
                                        results.screenReaderAnalysis.missingLabels.forEach(missing => {
                                            violations[missing.severity].push({
                                                type: 'Missing Label',
                                                element: missing.element,
                                                description: 'Element lacks accessible name for screen readers',
                                                recommendation: missing.recommendation,
                                                wcagPrinciple: 'Perceivable'
                                            });
                                        });
                                        
                                        results.screenReaderAnalysis.incorrectRoles.forEach(incorrect => {
                                            violations[incorrect.severity].push({
                                                type: 'Incorrect Role',
                                                element: incorrect.element,
                                                description: `Element has role '${incorrect.currentRole}' but expected '${incorrect.expectedRole.join(' or ')}'`,
                                                recommendation: `Update role attribute to match expected semantic meaning`,
                                                wcagPrinciple: 'Robust'
                                            });
                                        });
                                        
                                        // Keyboard navigation violations
                                        results.keyboardNavigationAnalysis.missingFocusIndicators.forEach(missing => {
                                            violations.major.push({
                                                type: 'Missing Focus Indicator',
                                                element: missing.element,
                                                description: 'Element lacks visible focus indicator',
                                                recommendation: missing.recommendation,
                                                wcagPrinciple: 'Operable'
                                            });
                                        });
                                        
                                        // Material-specific violations
                                        Object.entries(results.materialSpecificTests).forEach(([testName, test]) => {
                                            if (test.tested) {
                                                test.issues.forEach(issue => {
                                                    issue.issues.forEach(issueText => {
                                                        violations.major.push({
                                                            type: 'Material Component Issue',
                                                            element: issue.element,
                                                            description: issueText,
                                                            recommendation: 'Follow Angular Material accessibility guidelines',
                                                            wcagPrinciple: 'Understandable'
                                                        });
                                                    });
                                                });
                                            }
                                        });
                                        
                                        // Calculate WCAG principles summary
                                        const principlesSummary = {
                                            perceivable: { passed: 0, failed: 0, warnings: 0 },
                                            operable: { passed: 0, failed: 0, warnings: 0 },
                                            understandable: { passed: 0, failed: 0, warnings: 0 },
                                            robust: { passed: 0, failed: 0, warnings: 0 }
                                        };
                                        
                                        [...violations.critical, ...violations.major, ...violations.minor].forEach(violation => {
                                            const principle = violation.wcagPrinciple.toLowerCase();
                                            if (principlesSummary[principle]) {
                                                if (violation.type.includes('Critical')) {
                                                    principlesSummary[principle].failed++;
                                                } else if (violation.type.includes('Major')) {
                                                    principlesSummary[principle].failed++;
                                                } else {
                                                    principlesSummary[principle].warnings++;
                                                }
                                            }
                                        });
                                        
                                        // Count passed items
                                        principlesSummary.perceivable.passed = results.colorContrastAnalysis.passedElements.length;
                                        principlesSummary.operable.passed = results.keyboardNavigationAnalysis.focusableElements.filter(el => el.hasVisibleFocus).length;
                                        principlesSummary.understandable.passed = results.screenReaderAnalysis.ariaLabels.length;
                                        principlesSummary.robust.passed = results.materialComponentsAnalysis.totalComponents - violations.critical.length - violations.major.length;
                                        
                                        results.accessibilityViolations = {
                                            critical: violations.critical,
                                            major: violations.major,
                                            minor: violations.minor,
                                            summary: principlesSummary
                                        };
                                        
                                        // Update test summary
                                        results.testSummary.totalViolations = violations.critical.length + violations.major.length + violations.minor.length;
                                        results.testSummary.criticalViolations = violations.critical.length;
                                        
                                        // Calculate overall compliance score
                                        const totalTests = results.materialComponentsAnalysis.totalComponents || 1;
                                        const passedTests = totalTests - results.testSummary.totalViolations;
                                        results.testSummary.overallComplianceScore = Math.max(0, Math.round((passedTests / totalTests) * 100));
                                        
                                        // Update components analysis
                                        results.materialComponentsAnalysis.accessibleComponents = passedTests;
                                        results.materialComponentsAnalysis.componentsWithIssues = results.testSummary.totalViolations;
                                    };

                                    // Step 8: Generate Recommendations
                                    const generateRecommendations = () => {
                                        if (!
                """ + generateRecommendations.ToString().ToLower() + """
                                                                     ) return;
                                                                                             
                                                                                             const recommendations = {
                                                                                                 immediate: [],
                                                                                                 shortTerm: [],
                                                                                                 longTerm: [],
                                                                                                 bestPractices: []
                                                                                             };
                                                                                             
                                                                                             // Immediate recommendations (critical issues)
                                                                                             if (results.testSummary.criticalViolations > 0) {
                                                                                                 recommendations.immediate.push({
                                                                                                     priority: 'critical',
                                                                                                     title: 'Fix Critical Color Contrast Issues',
                                                                                                     description: `${results.accessibilityViolations.critical.length} critical accessibility violations detected`,
                                                                                                     action: 'Review and fix all color contrast ratios below 3:1',
                                                                                                     effort: 'high',
                                                                                                     impact: 'high'
                                                                                                 });
                                                                                             }
                                                                                             
                                                                                             if (results.screenReaderAnalysis.missingLabels.length > 0) {
                                                                                                 recommendations.immediate.push({
                                                                                                     priority: 'high',
                                                                                                     title: 'Add Missing ARIA Labels',
                                                                                                     description: `${results.screenReaderAnalysis.missingLabels.length} components missing accessible names`,
                                                                                                     action: 'Add aria-label or aria-labelledby attributes to all interactive elements',
                                                                                                     effort: 'medium',
                                                                                                     impact: 'high'
                                                                                                 });
                                                                                             }
                                                                                             
                                                                                             // Short-term recommendations
                                                                                             if (results.keyboardNavigationAnalysis.navigationScore < 80) {
                                                                                                 recommendations.shortTerm.push({
                                                                                                     priority: 'medium',
                                                                                                     title: 'Improve Keyboard Navigation',
                                                                                                     description: `Navigation score: ${results.keyboardNavigationAnalysis.navigationScore}%`,
                                                                                                     action: 'Add visible focus indicators and verify tab order',
                                                                                                     effort: 'medium',
                                                                                                     impact: 'medium'
                                                                                                 });
                                                                                             }
                                                                                             
                                                                                             if (Object.values(results.materialSpecificTests).some(test => test.failed > 0)) {
                                                                                                 recommendations.shortTerm.push({
                                                                                                     priority: 'medium',
                                                                                                     title: 'Review Material Component Usage',
                                                                                                     description: 'Some Material components have accessibility issues',
                                                                                                     action: 'Follow Angular Material accessibility guidelines for each component type',
                                                                                                     effort: 'medium',
                                                                                                     impact: 'medium'
                                                                                                 });
                                                                                             }
                                                                                             
                                                                                             // Long-term recommendations
                                                                                             recommendations.longTerm.push({
                                                                                                 priority: 'low',
                                                                                                 title: 'Implement Automated Accessibility Testing',
                                                                                                 description: 'Set up continuous accessibility monitoring',
                                                                                                 action: 'Integrate tools like axe-core into your CI/CD pipeline',
                                                                                                 effort: 'high',
                                                                                                 impact: 'high'
                                                                                             });
                                                                                             
                                                                                             recommendations.longTerm.push({
                                                                                                 priority: 'low',
                                                                                                 title: 'Accessibility Training',
                                                                                                 description: 'Educate development team on accessibility best practices',
                                                                                                 action: 'Conduct accessibility workshops and create internal guidelines',
                                                                                                 effort: 'high',
                                                                                                 impact: 'high'
                                                                                             });
                                                                                             
                                                                                             // Best practices
                                                                                             recommendations.bestPractices = [
                                                                                                 {
                                                                                                     category: 'Development',
                                                                                                     practice: 'Use semantic HTML elements',
                                                                                                     description: 'Leverage HTML semantics before adding ARIA attributes'
                                                                                                 },
                                                                                                 {
                                                                                                     category: 'Testing',
                                                                                                     practice: 'Test with real assistive technology',
                                                                                                     description: 'Use screen readers and keyboard-only navigation during development'
                                                                                                 },
                                                                                                 {
                                                                                                     category: 'Design',
                                                                                                     practice: 'Design with accessibility in mind',
                                                                                                     description: 'Consider color contrast, touch targets, and focus states in designs'
                                                                                                 },
                                                                                                 {
                                                                                                     category: 'Material Design',
                                                                                                     practice: 'Follow Material Design accessibility guidelines',
                                                                                                     description: 'Use Material guidelines for consistent accessible experiences'
                                                                                                 }
                                                                                             ];
                                                                                             
                                                                                             results.recommendations = recommendations;
                                                                                         };

                                                                                         // Step 9: Generate Compliance Report
                                                                                         const generateComplianceReport = () => {
                                                                                             const wcagPrinciples = {
                                                                                                 perceivable: {
                                                                                                     score: 0,
                                                                                                     guidelines: [
                                                                                                         { name: '1.4.3 Contrast (Minimum)', status: 'unknown', tests: ['Color contrast analysis'] },
                                                                                                         { name: '1.1.1 Non-text Content', status: 'unknown', tests: ['Alt text, ARIA labels'] },
                                                                                                         { name: '1.3.1 Info and Relationships', status: 'unknown', tests: ['Semantic structure'] }
                                                                                                     ]
                                                                                                 },
                                                                                                 operable: {
                                                                                                     score: 0,
                                                                                                     guidelines: [
                                                                                                         { name: '2.1.1 Keyboard', status: 'unknown', tests: ['Keyboard navigation'] },
                                                                                                         { name: '2.4.7 Focus Visible', status: 'unknown', tests: ['Focus indicators'] },
                                                                                                         { name: '2.1.2 No Keyboard Trap', status: 'unknown', tests: ['Keyboard trap detection'] }
                                                                                                     ]
                                                                                                 },
                                                                                                 understandable: {
                                                                                                     score: 0,
                                                                                                     guidelines: [
                                                                                                         { name: '3.2.1 On Focus', status: 'unknown', tests: ['Focus behavior'] },
                                                                                                         { name: '3.3.2 Labels or Instructions', status: 'unknown', tests: ['Form labels'] }
                                                                                                     ]
                                                                                                 },
                                                                                                 robust: {
                                                                                                     score: 0,
                                                                                                     guidelines: [
                                                                                                         { name: '4.1.2 Name, Role, Value', status: 'unknown', tests: ['ARIA implementation'] },
                                                                                                         { name: '4.1.1 Parsing', status: 'unknown', tests: ['Valid HTML'] }
                                                                                                     ]
                                                                                                 }
                                                                                             };
                                                                                             
                                                                                             // Update principle scores based on test results
                                                                                             const summary = results.accessibilityViolations.summary;
                                                                                             Object.keys(wcagPrinciples).forEach(principle => {
                                                                                                 const principleData = summary[principle];
                                                                                                 const total = principleData.passed + principleData.failed + principleData.warnings;
                                                                                                 if (total > 0) {
                                                                                                     wcagPrinciples[principle].score = Math.round((principleData.passed / total) * 100);
                                                                                                 } else {
                                                                                                     wcagPrinciples[principle].score = 100;
                                                                                                 }
                                                                                             });
                                                                                             
                                                                                             const materialDesignCompliance = {
                                                                                                 componentUsage: 'compliant',
                                                                                                 themeImplementation: 'unknown',
                                                                                                 interactionPatterns: 'needs_review',
                                                                                                 recommendations: [
                                                                                                     'Follow Material Design accessibility specifications',
                                                                                                     'Use Material theme with proper contrast ratios',
                                                                                                     'Implement standard Material interaction patterns'
                                                                                                 ]
                                                                                             };
                                                                                             
                                                                                             const actionPlan = [
                                                                                                 {
                                                                                                     phase: 'Phase 1: Critical Fixes',
                                                                                                     duration: '1-2 weeks',
                                                                                                     priority: 'high',
                                                                                                     tasks: [
                                                                                                         'Fix all critical color contrast violations',
                                                                                                         'Add missing ARIA labels to interactive elements',
                                                                                                         'Resolve keyboard navigation blocking issues'
                                                                                                     ]
                                                                                                 },
                                                                                                 {
                                                                                                     phase: 'Phase 2: Component Review',
                                                                                                     duration: '2-3 weeks',
                                                                                                     priority: 'medium',
                                                                                                     tasks: [
                                                                                                         'Review all Material component implementations',
                                                                                                         'Add comprehensive ARIA attributes',
                                                                                                         'Implement consistent focus management'
                                                                                                     ]
                                                                                                 },
                                                                                                 {
                                                                                                     phase: 'Phase 3: Testing & Validation',
                                                                                                     duration: '1-2 weeks',
                                                                                                     priority: 'medium',
                                                                                                     tasks: [
                                                                                                         'Conduct manual testing with assistive technology',
                                                                                                         'Set up automated accessibility testing',
                                                                                                         'Create accessibility testing guidelines'
                                                                                                     ]
                                                                                                 },
                                                                                                 {
                                                                                                     phase: 'Phase 4: Ongoing Maintenance',
                                                                                                     duration: 'ongoing',
                                                                                                     priority: 'low',
                                                                                                     tasks: [
                                                                                                         'Regular accessibility audits',
                                                                                                         'Team training and education',
                                                                                                         'Update guidelines as Material Design evolves'
                                                                                                     ]
                                                                                                 }
                                                                                             ];
                                                                                             
                                                                                             results.complianceReport = {
                                                                                                 wcagPrinciples: wcagPrinciples,
                                                                                                 materialDesignCompliance: materialDesignCompliance,
                                                                                                 actionPlan: actionPlan
                                                                                             };
                                                                                         };

                                                                                         // Execute the analysis workflow
                                                                                         try {
                                                                                             if (!detectFrameworks()) {
                                                                                                 results.testSummary.testStatus = 'no_material_detected';
                                                                                                 results.testSummary.overallComplianceScore = 0;
                                                                                                 return results;
                                                                                             }
                                                                                             
                                                                                             analyzeMaterialComponents();
                                                                                             
                                                                                             if (results.materialComponentsAnalysis.totalComponents === 0) {
                                                                                                 results.testSummary.testStatus = 'no_components_found';
                                                                                                 results.testSummary.overallComplianceScore = 100;
                                                                                                 return results;
                                                                                             }
                                                                                             
                                                                                             analyzeColorContrast();
                                                                                             analyzeKeyboardNavigation();
                                                                                             analyzeScreenReader();
                                                                                             testMaterialSpecificAccessibility();
                                                                                             generateViolationsSummary();
                                                                                             generateRecommendations();
                                                                                             generateComplianceReport();
                                                                                             
                                                                                             results.testSummary.testStatus = 'completed';
                                                                                             
                                                                                             return results;
                                                                                             
                                                                                         } catch (error) {
                                                                                             results.testSummary.testStatus = 'error';
                                                                                             results.testSummary.error = error.message;
                                                                                             return results;
                                                                                         }
                                                                                     })();
                                                                                 
                                                                     """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to validate Material accessibility compliance: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Extract Angular Material theme and design token information for accessibility analysis. See skills/playwright-mcp/tools/angular/material-accessibility-testing.md.")]
    public async Task<string> ExtractAngularMaterialTheme(
        string sessionId = "default")
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            var jsCode = """

                                         (() => {
                                             const themeInfo = {
                                                 detectionSummary: {
                                                     materialDetected: false,
                                                     themeDetected: false,
                                                     customTheme: false,
                                                     themeType: 'unknown',
                                                     version: 'unknown'
                                                 },
                                                 colorPalette: {
                                                     primary: {},
                                                     accent: {},
                                                     warn: {},
                                                     background: {},
                                                     foreground: {}
                                                 },
                                                 typography: {
                                                     fontFamily: 'unknown',
                                                     headings: {},
                                                     body: {},
                                                     accessibility: {
                                                         baseFontSize: 'unknown',
                                                         lineHeight: 'unknown',
                                                         scalability: 'unknown'
                                                     }
                                                 },
                                                 accessibilityFeatures: {
                                                     highContrast: false,
                                                     forcedColors: false,
                                                     reducedMotion: false,
                                                     colorSchemePreference: 'unknown'
                                                 },
                                                 customProperties: [],
                                                 recommendations: []
                                             };

                                             // Detect Material and theme
                                             const materialElements = document.querySelectorAll('[class*=mat-], mat-button, mat-card, mat-toolbar');
                                             themeInfo.detectionSummary.materialDetected = materialElements.length > 0;
                                             
                                             if (!themeInfo.detectionSummary.materialDetected) {
                                                 return themeInfo;
                                             }

                                             // Try to extract theme information
                                             const styles = window.getComputedStyle(document.body);
                                             
                                             // Check for Material theme CSS custom properties
                                             const materialProperties = [];
                                             for (let i = 0; i < document.styleSheets.length; i++) {
                                                 try {
                                                     const sheet = document.styleSheets[i];
                                                     if (sheet.cssRules) {
                                                         for (let j = 0; j < sheet.cssRules.length; j++) {
                                                             const rule = sheet.cssRules[j];
                                                             if (rule.style) {
                                                                 for (let k = 0; k < rule.style.length; k++) {
                                                                     const property = rule.style[k];
                                                                     if (property.startsWith('--mat-') || property.startsWith('--mdc-')) {
                                                                         materialProperties.push({
                                                                             property: property,
                                                                             value: rule.style.getPropertyValue(property).trim()
                                                                         });
                                                                     }
                                                                 }
                                                             }
                                                         }
                                                     }
                                                 } catch (e) {
                                                     // Skip inaccessible stylesheets
                                                 }
                                             }
                                             
                                             themeInfo.customProperties = materialProperties.slice(0, 50); // Limit for performance
                                             themeInfo.detectionSummary.themeDetected = materialProperties.length > 0;
                                             
                                             // Extract color information
                                             if (materialElements.length > 0) {
                                                 const sampleElement = materialElements[0];
                                                 const elementStyles = window.getComputedStyle(sampleElement);
                                                 
                                                 themeInfo.colorPalette.primary = {
                                                     color: elementStyles.getPropertyValue('--mat-toolbar-container-background-color') || 
                                                            elementStyles.getPropertyValue('--mat-primary-color') || 'unknown',
                                                     contrastText: elementStyles.getPropertyValue('--mat-toolbar-container-text-color') || 
                                                                  elementStyles.getPropertyValue('--mat-primary-contrast-color') || 'unknown'
                                                 };
                                                 
                                                 themeInfo.colorPalette.background = {
                                                     default: styles.backgroundColor || 'unknown',
                                                     paper: elementStyles.getPropertyValue('--mat-card-container-color') || 'unknown'
                                                 };
                                             }
                                             
                                             // Typography analysis
                                             themeInfo.typography.fontFamily = styles.fontFamily || 'unknown';
                                             themeInfo.typography.accessibility.baseFontSize = styles.fontSize || 'unknown';
                                             themeInfo.typography.accessibility.lineHeight = styles.lineHeight || 'unknown';
                                             
                                             // Check accessibility features
                                             const mediaQueries = {
                                                 highContrast: window.matchMedia('(prefers-contrast: high)').matches,
                                                 reducedMotion: window.matchMedia('(prefers-reduced-motion: reduce)').matches,
                                                 colorScheme: window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
                                             };
                                             
                                             themeInfo.accessibilityFeatures = {
                                                 highContrast: mediaQueries.highContrast,
                                                 forcedColors: window.matchMedia('(forced-colors: active)').matches,
                                                 reducedMotion: mediaQueries.reducedMotion,
                                                 colorSchemePreference: mediaQueries.colorScheme
                                             };
                                             
                                             // Generate recommendations
                                             themeInfo.recommendations = [
                                                 {
                                                     category: 'Theme Setup',
                                                     recommendation: 'Ensure proper Material Design theme configuration',
                                                     priority: 'high'
                                                 },
                                                 {
                                                     category: 'Accessibility',
                                                     recommendation: 'Test theme with high contrast and dark mode preferences',
                                                     priority: 'medium'
                                                 },
                                                 {
                                                     category: 'Typography',
                                                     recommendation: 'Verify font sizes meet minimum accessibility requirements',
                                                     priority: 'medium'
                                                 }
                                             ];
                                             
                                             return themeInfo;
                                         })();
                                     
                         """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return $"Failed to extract Angular Material theme: {ex.Message}";
        }
    }
}
