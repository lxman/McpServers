# Angular Style Tools

Analyze Angular component styling and detect component isolation issues.

## Methods

### AnalyzeAngularComponentStyles
Analyze Angular component styling and detect component isolation issues.

**Parameters:**
- `componentSelector` (string, required): Component selector or data-testid
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with component styling analysis

---

### ValidateAngularStylingBestPractices
Validate Angular component styling best practices.

**Parameters:**
- `componentSelector` (string, required): Component selector or data-testid
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with styling best practices validation

## Use Cases

- Component style isolation verification
- ViewEncapsulation validation
- CSS architecture review
- Style bleed detection
- Theme consistency checking
- Component styling optimization

## Analysis Includes

- **ViewEncapsulation**: Verify proper encapsulation mode
- **Style Isolation**: Check for style leakage
- **CSS Specificity**: Identify overly specific selectors
- **Global Styles**: Detect global style dependencies
- **Theme Compatibility**: Verify theme token usage
- **Performance**: Identify style-related performance issues

## Best Practices

1. Use ViewEncapsulation.Emulated (default) for isolation
2. Avoid deep selectors (::ng-deep) when possible
3. Use CSS custom properties for theming
4. Keep component styles scoped
5. Use :host for component root styling
6. Leverage Angular Material's theming system
