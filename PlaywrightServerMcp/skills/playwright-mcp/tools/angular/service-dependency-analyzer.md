# Angular Service Dependency Analyzer

Analyze Angular service dependency graph with comprehensive DI hierarchy mapping and service relationship visualization.

## Methods

### AnalyzeServiceDependencyGraph
Analyzes Angular service dependency graph with comprehensive DI hierarchy mapping.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `maxServices` (int, default: 50): Maximum number of services to analyze
- `includeDetailedAnalysis` (bool, default: true): Include detailed analysis
- `analyzeProviders` (bool, default: true): Analyze provider patterns
- `generateVisualization` (bool, default: true): Generate visualization data

**Returns:** string - JSON with service dependency graph analysis

## Use Cases

- Service dependency visualization
- Circular dependency detection in services
- DI hierarchy analysis
- Provider pattern validation
- Service architecture review
- Optimization opportunity identification

## Best Practices

1. Start with maxServices=50, increase if needed
2. Enable all analysis options for first run
3. Look for circular dependencies
4. Review singleton vs transient providers
5. Check for over-injected services
6. Identify services that could be tree-shakeable
