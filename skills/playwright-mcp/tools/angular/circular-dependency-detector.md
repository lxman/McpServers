# Angular Circular Dependency Detector

Detect and analyze circular dependencies in Angular applications with resolution strategies.

## Methods

### DetectCircularDependencies
Detect circular dependencies in Angular application.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `detectionMethod` (string, default: "comprehensive"): Detection method - dfs, tarjan, or comprehensive
- `includeResolutionSuggestions` (bool, default: true): Include detailed resolution suggestions
- `includePerformanceAnalysis` (bool, default: true): Include performance impact analysis
- `analyzeArchitectureImpact` (bool, default: true): Analyze architecture impact

**Returns:** string - JSON with circular dependency analysis

**Result Structure:**
```json
{
  "circularDependencies": [
    {
      "cycle": ["ServiceA", "ServiceB", "ServiceC", "ServiceA"],
      "severity": "high",
      "performanceImpact": "Potential initialization deadlock",
      "resolutionStrategy": "Introduce interface or extract shared logic"
    }
  ],
  "summary": {
    "totalCycles": 3,
    "servicesAffected": 8,
    "modulesAffected": 2
  },
  "recommendations": [
    "Break ServiceA -> ServiceB dependency using events",
    "Extract shared types into separate file"
  ]
}
```

## Detection Methods

**DFS (Depth-First Search)**
- Fast detection
- Basic cycle identification
- Good for quick checks

**Tarjan's Algorithm**
- Finds strongly connected components
- More thorough analysis
- Identifies all cycles efficiently

**Comprehensive**
- Combines multiple algorithms
- Full dependency graph analysis
- Detailed architecture insights
- Performance impact assessment

## Common Circular Dependency Patterns

1. **Service ↔ Service**: Two services injecting each other
2. **Component ↔ Service**: Component injected into service
3. **Module ↔ Module**: Circular module imports
4. **Deep Cycles**: A → B → C → A chains

## Resolution Strategies

The detector suggests:
- **Interface extraction**: Define shared interfaces
- **Event-based communication**: Use observables/subjects
- **Dependency inversion**: Introduce abstractions
- **Code splitting**: Extract shared logic to separate module
- **Lazy loading**: Break dependencies through lazy modules

## Performance Impact

Circular dependencies can cause:
- Initialization deadlocks
- Undefined references at runtime
- Increased bundle size
- Slower application startup
- Memory leaks

## Example Usage

```
# Detect circular dependencies
playwright:detect_circular_dependencies \
  --sessionId angular-test \
  --detectionMethod comprehensive \
  --includeResolutionSuggestions true

# Result shows:
# - All circular dependency cycles
# - Severity assessment
# - Resolution strategies
# - Architecture recommendations
```

## Integration with CI/CD

Add to build pipeline:
```yaml
- name: Check Circular Dependencies
  run: |
    # Run detection
    # Fail build if high-severity cycles found
```

## Notes

- Analyzes Angular DI graph
- Checks service dependencies
- Validates module structure
- Provides resolution guides
- Essential for maintainable architecture
- Run regularly in CI/CD
