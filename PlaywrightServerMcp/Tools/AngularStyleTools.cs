using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Playwright.Core.Services;

namespace PlaywrightServerMcp.Tools;

/// <summary>
/// Focused Angular styling analysis, Material theme extraction, and visual reporting tools
/// Follows Single Responsibility Principle - handles only styling-related functionality
/// Part of the modular Angular enhancement architecture (ANG-002 cleanup)
/// </summary>
[McpServerToolType]
public class AngularStyleTools(PlaywrightSessionManager sessionManager)
{
[McpServerTool]
    [Description("Analyze Angular component styling and detect component isolation issues. See skills/playwright-mcp/tools/angular/style-tools.md.")]
    public async Task<string> AnalyzeAngularComponentStyles(
        string componentSelector,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string finalSelector = DetermineSelector(componentSelector);
            
            // Clean JavaScript for Angular component style analysis
            var jsCode = $$"""
                (() => {
                    const component = document.querySelector('{{finalSelector.Replace("'", "\\'")}}');
                    if (!component) {
                        return { error: 'Component not found' };
                    }
                    
                    // Analyze Angular-specific styling
                    const ngAttributes = [];
                    Array.from(component.attributes).forEach(attr => {
                        if (attr.name.startsWith('ng-') || attr.name.startsWith('_ng')) {
                            ngAttributes.push({ name: attr.name, value: attr.value });
                        }
                    });
                    
                    // Check for Angular component isolation
                    const hasViewEncapsulation = ngAttributes.some(attr => 
                        attr.name.includes('_ng') && attr.name.includes('c'));
                    
                    // Analyze nested Angular components
                    const nestedComponents = Array.from(component.querySelectorAll('*'))
                        .filter(el => Array.from(el.attributes).some(attr => attr.name.startsWith('ng-') || attr.name.startsWith('_ng')))
                        .map(el => {
                            const elAttrs = Array.from(el.attributes)
                                .filter(attr => attr.name.startsWith('ng-') || attr.name.startsWith('_ng'))
                                .map(attr => ({ name: attr.name, value: attr.value }));
                            
                            return {
                                tagName: el.tagName.toLowerCase(),
                                selector: el.tagName.toLowerCase() + (el.id ? '#' + el.id : '') + 
                                         (el.className ? '.' + Array.from(el.classList).slice(0, 2).join('.') : ''),
                                ngAttributes: elAttrs,
                                computedStyles: {
                                    display: window.getComputedStyle(el).display,
                                    position: window.getComputedStyle(el).position,
                                    zIndex: window.getComputedStyle(el).zIndex
                                }
                            };
                        });
                    
                    // Check for CSS-in-JS or inline styles that might override Angular styles
                    const inlineStyleElements = Array.from(component.querySelectorAll('[style]'));
                    const inlineStyles = inlineStyleElements.map(el => {
                        return {
                            selector: el.tagName.toLowerCase() + (el.id ? '#' + el.id : ''),
                            inlineStyle: el.getAttribute('style'),
                            hasNgAttributes: Array.from(el.attributes).some(attr => attr.name.startsWith('ng-'))
                        };
                    });
                    
                    // Check for Material Design or other UI library components
                    const uiLibraryComponents = Array.from(component.querySelectorAll('*'))
                        .filter(el => {
                            const tagName = el.tagName.toLowerCase();
                            return tagName.startsWith('mat-') || 
                                   tagName.startsWith('ng-') || 
                                   tagName.startsWith('p-') || // PrimeNG
                                   tagName.startsWith('nz-') || // NG-ZORRO
                                   el.classList.toString().includes('mat-') ||
                                   el.classList.toString().includes('p-') ||
                                   el.classList.toString().includes('ant-');
                        })
                        .map(el => {
                            const computedStyles = window.getComputedStyle(el);
                            return {
                                tagName: el.tagName.toLowerCase(),
                                classes: Array.from(el.classList),
                                library: el.tagName.toLowerCase().startsWith('mat-') ? 'Angular Material' :
                                        el.tagName.toLowerCase().startsWith('p-') ? 'PrimeNG' :
                                        el.tagName.toLowerCase().startsWith('nz-') ? 'NG-ZORRO' :
                                        'Unknown',
                                theme: {
                                    primaryColor: computedStyles.getPropertyValue('--mdc-theme-primary') || 
                                                 computedStyles.getPropertyValue('--primary-color') || 'Not detected',
                                    surfaceColor: computedStyles.getPropertyValue('--mdc-theme-surface') || 
                                                 computedStyles.getPropertyValue('--surface-color') || 'Not detected'
                                }
                            };
                        });
                    
                    const componentStyles = window.getComputedStyle(component);
                    
                    return {
                        componentInfo: {
                            tagName: component.tagName.toLowerCase(),
                            selector: '{{finalSelector.Replace("'", "\\'")}}',
                            ngAttributes: ngAttributes,
                            hasViewEncapsulation: hasViewEncapsulation,
                            classes: Array.from(component.classList),
                            id: component.id || null
                        },
                        styling: {
                            display: componentStyles.display,
                            position: componentStyles.position,
                            backgroundColor: componentStyles.backgroundColor,
                            color: componentStyles.color,
                            fontFamily: componentStyles.fontFamily,
                            fontSize: componentStyles.fontSize,
                            padding: componentStyles.padding,
                            margin: componentStyles.margin,
                            border: componentStyles.border,
                            borderRadius: componentStyles.borderRadius,
                            boxShadow: componentStyles.boxShadow,
                            transform: componentStyles.transform,
                            opacity: componentStyles.opacity,
                            zIndex: componentStyles.zIndex
                        },
                        nestedComponents: nestedComponents,
                        inlineStyles: inlineStyles,
                        uiLibraryComponents: uiLibraryComponents,
                        analysis: {
                            hasInlineStyles: inlineStyles.length > 0,
                            nestedComponentCount: nestedComponents.length,
                            uiLibraryComponentCount: uiLibraryComponents.length,
                            detectedLibraries: [...new Set(uiLibraryComponents.map(comp => comp.library))],
                            potentialStyleConflicts: inlineStyles.filter(style => style.hasNgAttributes).length
                        }
                    };
                })();
                """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsComplex);
        }
        catch (Exception ex)
        {
            return $"Failed to analyze Angular component styles: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Extract Angular Material theme and design token information. See skills/playwright-mcp/tools/angular/style-tools.md.")]
    public async Task<string> ExtractAngularMaterialTheme(
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";
            
            var jsCode = """
                (() => {
                    // Extract Material Design theme tokens
                    const rootStyles = window.getComputedStyle(document.documentElement);
                    const materialTokens = {};
                    const mdcTokens = {};
                    const customTokens = {};
                    
                    // Iterate through all CSS custom properties
                    for (let prop of rootStyles) {
                        if (prop.startsWith('--')) {
                            const value = rootStyles.getPropertyValue(prop).trim();
                            
                            if (prop.includes('mat-') || prop.includes('material')) {
                                materialTokens[prop] = value;
                            } else if (prop.includes('mdc-')) {
                                mdcTokens[prop] = value;
                            } else if (prop.includes('primary') || prop.includes('accent') || 
                                      prop.includes('warn') || prop.includes('theme')) {
                                customTokens[prop] = value;
                            }
                        }
                    }
                    
                    // Check for Material components on the page
                    const materialComponents = Array.from(document.querySelectorAll('[class*="mat-"], mat-*'))
                        .map(el => {
                            const computedStyles = window.getComputedStyle(el);
                            return {
                                tagName: el.tagName.toLowerCase(),
                                classes: Array.from(el.classList).filter(cls => cls.includes('mat-')),
                                colors: {
                                    color: computedStyles.color,
                                    backgroundColor: computedStyles.backgroundColor,
                                    borderColor: computedStyles.borderColor
                                },
                                typography: {
                                    fontSize: computedStyles.fontSize,
                                    fontWeight: computedStyles.fontWeight,
                                    fontFamily: computedStyles.fontFamily
                                }
                            };
                        });
                    
                    // Extract theme-related colors from common Material elements
                    const themeColors = {};
                    const primaryElements = document.querySelectorAll('[color="primary"], .mat-primary, .mat-button-primary');
                    const accentElements = document.querySelectorAll('[color="accent"], .mat-accent, .mat-button-accent');
                    const warnElements = document.querySelectorAll('[color="warn"], .mat-warn, .mat-button-warn');
                    
                    if (primaryElements.length > 0) {
                        const primaryStyle = window.getComputedStyle(primaryElements[0]);
                        themeColors.primary = {
                            color: primaryStyle.color,
                            backgroundColor: primaryStyle.backgroundColor
                        };
                    }
                    
                    if (accentElements.length > 0) {
                        const accentStyle = window.getComputedStyle(accentElements[0]);
                        themeColors.accent = {
                            color: accentStyle.color,
                            backgroundColor: accentStyle.backgroundColor
                        };
                    }
                    
                    if (warnElements.length > 0) {
                        const warnStyle = window.getComputedStyle(warnElements[0]);
                        themeColors.warn = {
                            color: warnStyle.color,
                            backgroundColor: warnStyle.backgroundColor
                        };
                    }
                    
                    // Check for dark theme indicators
                    const isDarkTheme = document.body.classList.contains('dark-theme') ||
                                       document.body.classList.contains('mat-dark-theme') ||
                                       window.getComputedStyle(document.body).backgroundColor === 'rgb(48, 48, 48)' ||
                                       window.getComputedStyle(document.body).backgroundColor === 'rgb(33, 33, 33)';
                    
                    return {
                        materialDesignTokens: materialTokens,
                        mdcTokens: mdcTokens,
                        customThemeTokens: customTokens,
                        themeColors: themeColors,
                        materialComponents: materialComponents.slice(0, 20), // Limit output
                        themeAnalysis: {
                            isDarkTheme: isDarkTheme,
                            totalMaterialTokens: Object.keys(materialTokens).length,
                            totalMdcTokens: Object.keys(mdcTokens).length,
                            totalCustomTokens: Object.keys(customTokens).length,
                            materialComponentCount: materialComponents.length,
                            hasThemeColors: Object.keys(themeColors).length > 0
                        }
                    };
                })();
                """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsComplex);
        }
        catch (Exception ex)
        {
            return $"Failed to extract Angular Material theme: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Validate Angular component styling best practices. See skills/playwright-mcp/tools/angular/style-tools.md.")]
    public async Task<string> ValidateAngularStylingBestPractices(
        string componentSelector,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string finalSelector = DetermineSelector(componentSelector);
            
            var jsCode = $$"""
                (() => {
                    const component = document.querySelector('{{finalSelector.Replace("'", "\\'")}}');
                    if (!component) {
                        return { error: 'Component not found' };
                    }
                    
                    const violations = [];
                    const recommendations = [];
                    const goodPractices = [];
                    
                    // Check for inline styles (generally discouraged in Angular)
                    const elementsWithInlineStyles = Array.from(component.querySelectorAll('[style]'));
                    if (elementsWithInlineStyles.length > 0) {
                        violations.push({
                            type: 'inline-styles',
                            severity: 'medium',
                            message: `Found ${elementsWithInlineStyles.length} elements with inline styles`,
                            elements: elementsWithInlineStyles.slice(0, 5).map(el => el.tagName.toLowerCase()),
                            recommendation: 'Use CSS classes or Angular component styles instead of inline styles'
                        });
                    }
                    
                    // Check for !important declarations (code smell)
                    const allElements = Array.from(component.querySelectorAll('*'));
                    let importantCount = 0;
                    allElements.forEach(el => {
                        const inlineStyle = el.getAttribute('style');
                        if (inlineStyle && inlineStyle.includes('!important')) {
                            importantCount++;
                        }
                    });
                    
                    if (importantCount > 0) {
                        violations.push({
                            type: 'important-declarations',
                            severity: 'high',
                            message: `Found ${importantCount} elements using !important`,
                            recommendation: 'Avoid !important declarations. Use more specific selectors or restructure CSS'
                        });
                    }
                    
                    // Check for proper Angular Material usage
                    const matElements = Array.from(component.querySelectorAll('mat-*'));
                    const customStyledMatElements = matElements.filter(el => {
                        return el.hasAttribute('style') || 
                               Array.from(el.classList).some(cls => !cls.startsWith('mat-'));
                    });
                    
                    if (customStyledMatElements.length > 0) {
                        violations.push({
                            type: 'material-customization',
                            severity: 'medium',
                            message: `Found ${customStyledMatElements.length} Material components with custom styling`,
                            recommendation: 'Use Angular Material theming system instead of direct CSS overrides'
                        });
                    }
                    
                    // Check for responsive design patterns
                    const responsiveElements = allElements.filter(el => {
                        const computedStyles = window.getComputedStyle(el);
                        return computedStyles.display === 'flex' || 
                               computedStyles.display === 'grid' ||
                               [computedStyles.width, computedStyles.height, computedStyles.fontSize]
                                   .some(val => val.includes('vw') || val.includes('vh') || val.includes('%'));
                    });
                    
                    if (responsiveElements.length > 0) {
                        goodPractices.push({
                            type: 'responsive-design',
                            message: `Found ${responsiveElements.length} elements using responsive design patterns`,
                            details: 'Good use of flexbox, grid, or viewport units'
                        });
                    }
                    
                    // Check for proper semantic HTML
                    const semanticElements = Array.from(component.querySelectorAll('header, nav, main, section, article, aside, footer'));
                    if (semanticElements.length > 0) {
                        goodPractices.push({
                            type: 'semantic-html',
                            message: `Found ${semanticElements.length} semantic HTML elements`,
                            details: 'Good use of semantic HTML structure'
                        });
                    }
                    
                    // Check for CSS Grid or Flexbox layout
                    const layoutElements = allElements.filter(el => {
                        const computedStyles = window.getComputedStyle(el);
                        return computedStyles.display === 'flex' || computedStyles.display === 'grid';
                    });
                    
                    if (layoutElements.length > 0) {
                        goodPractices.push({
                            type: 'modern-layout',
                            message: `Found ${layoutElements.length} elements using modern CSS layout`,
                            details: 'Good use of CSS Grid or Flexbox'
                        });
                    }
                    
                    // Overall component analysis
                    const hasViewEncapsulation = Array.from(component.attributes)
                        .some(attr => attr.name.startsWith('_ng') && attr.name.includes('c'));
                    
                    if (hasViewEncapsulation) {
                        goodPractices.push({
                            type: 'view-encapsulation',
                            message: 'Component uses Angular View Encapsulation',
                            details: 'Styles are properly isolated to this component'
                        });
                    }
                    
                    // Calculate overall score
                    const violationScore = violations.reduce((score, v) => {
                        return score - (v.severity === 'high' ? 30 : v.severity === 'medium' ? 15 : 5);
                    }, 100);
                    
                    const practiceBonus = Math.min(goodPractices.length * 5, 20);
                    const finalScore = Math.max(0, Math.min(100, violationScore + practiceBonus));
                    
                    return {
                        componentSelector: '{{finalSelector.Replace("'", "\\'")}}',
                        violations: violations,
                        recommendations: recommendations,
                        goodPractices: goodPractices,
                        score: {
                            overall: finalScore,
                            grade: finalScore >= 90 ? 'A' : finalScore >= 80 ? 'B' : 
                                   finalScore >= 70 ? 'C' : finalScore >= 60 ? 'D' : 'F',
                            breakdown: {
                                violations: violations.length,
                                goodPractices: goodPractices.length,
                                hasViewEncapsulation: hasViewEncapsulation
                            }
                        },
                        summary: {
                            totalElements: allElements.length,
                            elementsWithInlineStyles: elementsWithInlineStyles.length,
                            materialElements: matElements.length,
                            responsiveElements: responsiveElements.length,
                            semanticElements: semanticElements.length
                        }
                    };
                })();
                """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsComplex);
        }
        catch (Exception ex)
        {
            return $"Failed to validate Angular styling best practices: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Capture visual element properties for detailed reporting to Claude. See skills/playwright-mcp/tools/angular/style-tools.md.")]
    public async Task<string> CaptureElementVisualReport(
        string selector,
        string sessionId = "default")
    {
        try
        {
            PlaywrightSessionManager.SessionContext? session = sessionManager.GetSession(sessionId);
            if (session?.Page == null)
                return $"Session {sessionId} not found or page not available.";

            string finalSelector = DetermineSelector(selector);
            
            var jsCode = $$"""
                (() => {
                    const element = document.querySelector('{{finalSelector.Replace("'", "\\'")}}');
                    if (!element) {
                        return { error: 'Element not found' };
                    }
                    
                    const computedStyles = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    
                    // Helper function to convert colors to different formats
                    function parseColor(colorString) {
                        const div = document.createElement('div');
                        div.style.color = colorString;
                        document.body.appendChild(div);
                        const computedColor = window.getComputedStyle(div).color;
                        document.body.removeChild(div);
                        
                        // Parse RGB values
                        const match = computedColor.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)/);
                        if (match) {
                            const r = parseInt(match[1]);
                            const g = parseInt(match[2]);
                            const b = parseInt(match[3]);
                            const a = match[4] ? parseFloat(match[4]) : 1;
                            
                            // Convert to hex
                            const hex = '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('');
                            
                            // Convert to HSL
                            const rNorm = r / 255;
                            const gNorm = g / 255;
                            const bNorm = b / 255;
                            const max = Math.max(rNorm, gNorm, bNorm);
                            const min = Math.min(rNorm, gNorm, bNorm);
                            const diff = max - min;
                            const l = (max + min) / 2;
                            
                            let h = 0;
                            let s = 0;
                            
                            if (diff !== 0) {
                                s = l > 0.5 ? diff / (2 - max - min) : diff / (max + min);
                                
                                switch (max) {
                                    case rNorm: h = (gNorm - bNorm) / diff + (gNorm < bNorm ? 6 : 0); break;
                                    case gNorm: h = (bNorm - rNorm) / diff + 2; break;
                                    case bNorm: h = (rNorm - gNorm) / diff + 4; break;
                                }
                                h /= 6;
                            }
                            
                            return {
                                original: colorString,
                                computed: computedColor,
                                rgb: { r, g, b, a },
                                hex: hex,
                                hsl: {
                                    h: Math.round(h * 360),
                                    s: Math.round(s * 100),
                                    l: Math.round(l * 100)
                                }
                            };
                        }
                        return { original: colorString, computed: computedColor };
                    }
                    
                    // Comprehensive visual report
                    const report = {
                        basicInfo: {
                            selector: '{{finalSelector.Replace("'", "\\'")}}',
                            tagName: element.tagName.toLowerCase(),
                            id: element.id || null,
                            classes: Array.from(element.classList),
                            textContent: element.textContent?.trim().substring(0, 200) || '',
                            innerHTML: element.innerHTML.substring(0, 300) || ''
                        },
                        dimensions: {
                            width: { 
                                pixels: rect.width,
                                css: computedStyles.width
                            },
                            height: { 
                                pixels: rect.height,
                                css: computedStyles.height
                            },
                            position: {
                                top: rect.top,
                                left: rect.left,
                                right: rect.right,
                                bottom: rect.bottom
                            }
                        },
                        colors: {
                            text: parseColor(computedStyles.color),
                            background: parseColor(computedStyles.backgroundColor),
                            border: parseColor(computedStyles.borderColor),
                            borderTop: parseColor(computedStyles.borderTopColor),
                            borderRight: parseColor(computedStyles.borderRightColor),
                            borderBottom: parseColor(computedStyles.borderBottomColor),
                            borderLeft: parseColor(computedStyles.borderLeftColor)
                        },
                        borders: {
                            style: computedStyles.borderStyle,
                            width: computedStyles.borderWidth,
                            radius: computedStyles.borderRadius,
                            individual: {
                                top: { width: computedStyles.borderTopWidth, style: computedStyles.borderTopStyle },
                                right: { width: computedStyles.borderRightWidth, style: computedStyles.borderRightStyle },
                                bottom: { width: computedStyles.borderBottomWidth, style: computedStyles.borderBottomStyle },
                                left: { width: computedStyles.borderLeftWidth, style: computedStyles.borderLeftStyle }
                            }
                        },
                        spacing: {
                            margin: {
                                all: computedStyles.margin,
                                top: computedStyles.marginTop,
                                right: computedStyles.marginRight,
                                bottom: computedStyles.marginBottom,
                                left: computedStyles.marginLeft
                            },
                            padding: {
                                all: computedStyles.padding,
                                top: computedStyles.paddingTop,
                                right: computedStyles.paddingRight,
                                bottom: computedStyles.paddingBottom,
                                left: computedStyles.paddingLeft
                            }
                        },
                        typography: {
                            fontFamily: computedStyles.fontFamily,
                            fontSize: computedStyles.fontSize,
                            fontWeight: computedStyles.fontWeight,
                            fontStyle: computedStyles.fontStyle,
                            lineHeight: computedStyles.lineHeight,
                            letterSpacing: computedStyles.letterSpacing,
                            wordSpacing: computedStyles.wordSpacing,
                            textAlign: computedStyles.textAlign,
                            textDecoration: computedStyles.textDecoration,
                            textTransform: computedStyles.textTransform,
                            textIndent: computedStyles.textIndent,
                            textShadow: computedStyles.textShadow
                        },
                        layout: {
                            display: computedStyles.display,
                            position: computedStyles.position,
                            float: computedStyles.float,
                            clear: computedStyles.clear,
                            overflow: computedStyles.overflow,
                            overflowX: computedStyles.overflowX,
                            overflowY: computedStyles.overflowY,
                            visibility: computedStyles.visibility,
                            opacity: computedStyles.opacity,
                            zIndex: computedStyles.zIndex
                        },
                        flexbox: {
                            flexDirection: computedStyles.flexDirection,
                            flexWrap: computedStyles.flexWrap,
                            justifyContent: computedStyles.justifyContent,
                            alignItems: computedStyles.alignItems,
                            alignContent: computedStyles.alignContent,
                            flexGrow: computedStyles.flexGrow,
                            flexShrink: computedStyles.flexShrink,
                            flexBasis: computedStyles.flexBasis,
                            alignSelf: computedStyles.alignSelf,
                            order: computedStyles.order
                        },
                        grid: {
                            gridTemplateColumns: computedStyles.gridTemplateColumns,
                            gridTemplateRows: computedStyles.gridTemplateRows,
                            gridTemplateAreas: computedStyles.gridTemplateAreas,
                            gridColumnGap: computedStyles.gridColumnGap,
                            gridRowGap: computedStyles.gridRowGap,
                            gridColumn: computedStyles.gridColumn,
                            gridRow: computedStyles.gridRow,
                            gridArea: computedStyles.gridArea
                        },
                        effects: {
                            boxShadow: computedStyles.boxShadow,
                            transform: computedStyles.transform,
                            transformOrigin: computedStyles.transformOrigin,
                            transition: computedStyles.transition,
                            animation: computedStyles.animation,
                            filter: computedStyles.filter,
                            backdropFilter: computedStyles.backdropFilter
                        },
                        state: {
                            isVisible: computedStyles.visibility === 'visible' && computedStyles.display !== 'none',
                            isInteractive: ['button', 'a', 'input', 'textarea', 'select'].includes(element.tagName.toLowerCase()) ||
                                          element.hasAttribute('onclick') || 
                                          element.style.cursor === 'pointer',
                            hasHover: false, // Would need to simulate hover state
                            hasFocus: element === document.activeElement,
                            isDisabled: element.hasAttribute('disabled') || element.getAttribute('aria-disabled') === 'true'
                        },
                        accessibility: {
                            ariaLabel: element.getAttribute('aria-label'),
                            ariaRole: element.getAttribute('role'),
                            tabIndex: element.getAttribute('tabindex'),
                            title: element.getAttribute('title'),
                            alt: element.getAttribute('alt')
                        }
                    };
                    
                    return report;
                })();
                """;

            var result = await session.Page.EvaluateAsync<object>(jsCode);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsComplex);
        }
        catch (Exception ex)
        {
            return $"Failed to capture element visual report: {ex.Message}";
        }
    }

    /// <summary>
    /// Helper method for smart selector determination - determines if input is a CSS selector or data-testid
    /// </summary>
    private static string DetermineSelector(string selector)
    {
        if (selector.Contains('[') || selector.Contains('.') || selector.Contains('#') || 
            selector.Contains('>') || selector.Contains(' ') || selector.Contains(':'))
        {
            return selector;
        }
        
        if (!string.IsNullOrEmpty(selector) && !selector.Contains('='))
        {
            return $"[data-testid='{selector}']";
        }
        
        return selector;
    }
}
