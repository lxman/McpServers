# Accessibility Testing Tools

WCAG compliance testing, ARIA validation, keyboard navigation, and color contrast checking.

## Methods

### ValidateAriaLabels
Validate ARIA labels and attributes with comprehensive rules.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `containerSelector` (string?, optional): Optional container to limit analysis

**Returns:** string - JSON with ARIA validation results and violations

---

### TestAccessibility
Test accessibility features (keyboard navigation, screen reader, etc).

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `testType` (string, required): Test type - keyboard_navigation, aria_labels, color_contrast, focus_order

**Returns:** string - JSON with test results

---

### TestColorContrast
Test color contrast ratios against WCAG guidelines.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `ratio` (double, default: 4.5): Minimum contrast ratio (4.5 for AA, 3.0 for AA Large, 7.0 for AAA)

**Returns:** string - JSON with contrast violations

---

### TestFocusOrder
Test keyboard focus order and focus management.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `containerSelector` (string?, optional): Optional container to limit testing

**Returns:** string - JSON with focus order analysis

---

### TestScreenReaderNavigation
Test screen reader navigation patterns.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with screen reader compatibility results

## WCAG Levels

- **Level A**: Basic accessibility (minimum)
- **Level AA**: Standard compliance (recommended)
- **Level AAA**: Enhanced accessibility (optimal)

## Common Violations

- Missing alt text on images
- Insufficient color contrast
- Missing ARIA labels
- Improper heading hierarchy
- No keyboard navigation
- Missing focus indicators

## Testing Pattern

```
# 1. Validate ARIA
playwright:validate_aria_labels --sessionId test

# 2. Check contrast
playwright:test_color_contrast --ratio 4.5 --sessionId test

# 3. Test keyboard navigation
playwright:test_accessibility --testType keyboard_navigation --sessionId test

# 4. Check focus order
playwright:test_focus_order --sessionId test
```

## Notes

- WCAG 2.1 Level AA recommended minimum
- Color contrast critical for readability
- Keyboard navigation required for accessibility
- ARIA labels must be meaningful
