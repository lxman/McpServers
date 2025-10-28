# Visual Testing Tools

Screenshot capture, visual regression testing, and element styling analysis.

## Methods

### CaptureScreenshot
Capture full page or element screenshot.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `fullPage` (bool, default: false): Capture full scrollable page
- `selector` (string?, optional): Optional element selector to capture specific element

**Returns:** string - Path to saved screenshot file

---

### CaptureElementScreenshot
Capture screenshot of specific element.

**Parameters:**
- `selector` (string, required): Element selector (CSS or data-testid)
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Path to saved screenshot file

---

### CompareScreenshots
Visual regression testing with pixel comparison.

**Parameters:**
- `baselinePath` (string, required): Path to baseline screenshot (must be canonical)
- `sessionId` (string, default: "default"): Session ID
- `selector` (string?, optional): Optional element selector to compare specific element

**Returns:** string - JSON with comparison results including difference percentage

---

### AnalyzePageColors
Get detailed color analysis for elements (useful for design system validation).

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `containerSelector` (string?, optional): Optional CSS selector to limit analysis

**Returns:** string - JSON with color palette and usage statistics

---

### InspectElementStyles
Extract comprehensive CSS style information for a specific element.

**Parameters:**
- `selector` (string, required): Element selector
- `sessionId` (string, default: "default"): Session ID
- `includeAllStyles` (bool, default: false): Include all computed styles

**Returns:** string - JSON with element styles

---

### CompareElementStyles
Compare visual styles between multiple elements.

**Parameters:**
- `selectorsJson` (string, required): Array of element selectors (JSON format)
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with style comparison

---

### AnalyzeLayoutAnalyze layout for responsive design testing.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `containerSelector` (string?, optional): Optional container selector

**Returns:** string - JSON with layout analysis

---

### AnalyzeHoverEffects
Analyze hover effects and style changes.

**Parameters:**
- `selector` (string, required): Element selector
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with hover effect analysis

---

### ExtractDesignTokens
Extract design system information and CSS custom properties.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with design tokens (CSS variables, colors, spacing)

---

### CaptureElementVisualReport
Capture visual element properties for detailed reporting.

**Parameters:**
- `selector` (string, required): Element selector or data-testid
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with comprehensive visual properties

## Use Cases

- **Visual Regression**: Detect unintended UI changes
- **Design System Validation**: Verify color palette usage
- **Responsive Testing**: Check layout at different viewports
- **Style Debugging**: Inspect element styles
- **Design Token Extraction**: Document design system

## Notes

- Screenshots saved to temp directory
- Supports full page and element capture
- Pixel-perfect comparison available
- Design system analysis included
