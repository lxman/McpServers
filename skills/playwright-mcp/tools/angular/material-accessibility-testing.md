# Angular Material Accessibility Testing

Comprehensive WCAG testing for Angular Material components with keyboard navigation and screen reader validation.

## Methods

### ValidateMaterialAccessibilityCompliance
Validate Angular Material components for accessibility compliance.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID for browser context
- `componentSelector` (string, default: ""): Specific Material component selector (optional)
- `wcagLevel` (string, default: "AA"): WCAG compliance level (A, AA, AAA)
- `testKeyboardNavigation` (bool, default: true): Test keyboard navigation patterns
- `testScreenReader` (bool, default: true): Test screen reader compatibility
- `testColorContrast` (bool, default: true): Test color contrast ratios
- `includeDetails` (bool, default: true): Include detailed violation descriptions
- `generateRecommendations` (bool, default: true): Generate remediation recommendations

**Returns:** string - JSON with accessibility compliance results

---

### ExtractAngularMaterialTheme
Extract Angular Material theme and design token information.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID for browser context

**Returns:** string - JSON with Material theme information

## Use Cases

- WCAG 2.1 AA/AAA compliance testing
- Material component accessibility audits
- Theme token extraction
- Remediation recommendations

## Best Practices

1. Test at component level first, then application-wide
2. Use AA level as minimum standard
3. Include all three test types (keyboard, screen reader, contrast)
4. Generate recommendations for faster fixes
5. Re-test after implementing fixes
