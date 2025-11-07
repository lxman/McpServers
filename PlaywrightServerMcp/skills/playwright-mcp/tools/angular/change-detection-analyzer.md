# Angular Change Detection Analyzer

Monitor and analyze Angular change detection performance with bottleneck detection.

## Methods

### DetectChangeDetectionBottlenecks
Detect and analyze change detection bottlenecks.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds to analyze
- `maxComponents` (int, default: 25): Maximum number of components to analyze
- `severityThreshold` (string, default: "medium"): Severity threshold - low, medium, high
- `includeDetailedAnalysis` (bool, default: true): Include detailed bottleneck analysis

**Returns:** string - JSON with bottleneck analysis

**Result Structure:**
```json
{
  "bottlenecks": [
    {
      "component": "ProductListComponent",
      "detectionCount": 150,
      "avgDurationMs": 12.5,
      "severity": "high",
      "recommendation": "Use OnPush strategy"
    }
  ],
  "summary": {
    "totalDetections": 500,
    "avgDetectionTime": 5.2,
    "componentsAnalyzed": 15
  }
}
```

---

### AnalyzeChangeDetection
Monitor change detection cycles.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with change detection metrics

**Purpose:**
Track how often change detection runs and which components trigger it.

---

### ValidateChangeDetectionPatterns
Validate change detection patterns for zoneless compatibility.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `checkAntiPatterns` (bool, default: true): Check for common anti-patterns
- `analyzeHierarchy` (bool, default: true): Analyze component hierarchy for optimization

**Returns:** string - JSON with validation results

**Purpose:**
Prepare for Angular 18+ zoneless mode by identifying incompatible patterns.

## Performance Issues Detected

- **Default strategy overuse**: Components using Default instead of OnPush
- **Unnecessary bindings**: Template expressions that run too frequently
- **NgZone pollution**: Code triggering unnecessary change detection
- **Heavy computations**: Expensive operations in getters
- **Deep component trees**: Inefficient hierarchy structures

## Optimization Recommendations

The analyzer suggests:
- OnPush strategy adoption
- TrackBy functions for ngFor
- Pure pipes for transformations
- Detaching change detector
- Manual change detection
- Zoneless migration preparation

## Example Workflow

```
# Start monitoring
playwright:detect_change_detection_bottlenecks \
  --sessionId angular-test \
  --durationSeconds 30 \
  --severityThreshold medium

# Perform user actions while monitoring
playwright:click_element --selector "#load-data"
playwright:fill_field --selector "#search" --value "test"

# Get results with recommendations
```

## Notes

- Monitors Zone.js change detection cycles
- Tracks component-level performance
- Identifies OnPush candidates
- Provides actionable recommendations
- Essential for Angular 18+ zoneless migration
