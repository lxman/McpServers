# Angular Bundle Analyzer

Analyze Angular bundle size by component with detailed impact analysis and optimization recommendations.

## Methods

### AnalyzeBundleSizeByComponent
Analyze bundle size impact of each component.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID for context
- `workingDirectory` (string, default: ""): Working directory containing Angular project (defaults to current directory)
- `buildConfiguration` (string, default: "production"): Build configuration - production, development, or custom configuration name
- `maxComponents` (int, default: 50): Maximum number of components to analyze (default: 50)
- `includeComponentAnalysis` (bool, default: true): Include detailed component analysis
- `includeDependencyAnalysis` (bool, default: true): Include dependency analysis
- `includeAssetAnalysis` (bool, default: true): Include asset analysis
- `generateRecommendations` (bool, default: true): Generate optimization recommendations

**Returns:** string - JSON with BundleSizeAnalysisResult

**Result Structure:**
```json
{
  "bundleOverview": {
    "totalSize": 1024000,
    "gzippedSize": 256000,
    "mainBundleSize": 512000,
    "lazyLoadedChunks": 5
  },
  "componentImpacts": [
    {
      "componentName": "AppComponent",
      "sizeBytes": 15000,
      "percentageOfTotal": 1.5,
      "dependencies": ["CommonModule", "RouterModule"]
    }
  ],
  "optimizationRecommendations": [
    {
      "priority": "High",
      "type": "LazyLoading",
      "description": "Consider lazy loading AdminModule"
    }
  ]
}
```

**Example:**
```
playwright:analyze_bundle_size_by_component \
  --workingDirectory "/path/to/angular/project" \
  --buildConfiguration production \
  --maxComponents 50
```

---

### CompareBuilds
Compare two build configurations.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `workingDirectory` (string, default: ""): Working directory
- `config1` (string, required): First configuration name
- `config2` (string, required): Second configuration name

**Returns:** string - JSON with comparison results

**Purpose:**
Compare bundle sizes between configurations (e.g., development vs production).

## Use Cases

1. **CI/CD Integration**: Check for bundle size regressions
2. **Optimization**: Identify largest components
3. **Code Review**: Validate optimization efforts
4. **Monitoring**: Track bundle size over time

## Optimization Insights

The analyzer provides:
- Component-level size breakdown
- Lazy loading opportunities
- Unused dependency detection
- Asset optimization suggestions
- Tree-shaking effectiveness
- Third-party library impact

## Budget Violations

Detects when bundles exceed configured budgets in angular.json:
```json
{
  "budgets": [
    {
      "type": "bundle",
      "name": "main",
      "baseline": "500kb",
      "maximumWarning": "1mb",
      "maximumError": "2mb"
    }
  ]
}
```

## Notes

- Requires angular.json in working directory
- Runs actual Angular CLI build
- Analysis takes 1-3 minutes
- Results include gzipped sizes
- Provides actionable recommendations
