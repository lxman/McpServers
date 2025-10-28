# Angular Component Analyzer

Analyze Angular component tree, hierarchy, inputs, outputs, and dependencies.

## Methods

### GetAngularComponentTree
Enhanced Angular component hierarchy analysis with Angular 17+ support.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with component tree structure

**Result Includes:**
- Component hierarchy
- Standalone components detection
- Signals usage (Angular 16+)
- Inputs and Outputs
- Change detection strategies
- Dependencies

---

### AnalyzeAngularComponentStyles
Analyze Angular component styling and detect component isolation issues.

**Parameters:**
- `componentSelector` (string, required): Component selector or data-testid
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with styling analysis

**Analyzes:**
- ViewEncapsulation mode
- Style isolation
- CSS scope
- Shadow DOM usage
- Style conflicts

---

### ValidateAngularStylingBestPractices
Validate Angular component styling best practices.

**Parameters:**
- `componentSelector` (string, required): Component selector
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with validation results

## Use Case

```
# Get component structure
playwright:get_angular_component_tree --sessionId test

# Analyze specific component
playwright:analyze_angular_component_styles \
  --componentSelector "app-header" \
  --sessionId test
```
