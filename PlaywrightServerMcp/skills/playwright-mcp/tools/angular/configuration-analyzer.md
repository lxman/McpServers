# Angular Configuration Analyzer

Analyze angular.json configuration with validation and best practices.

## Methods

### AnalyzeAngularJsonConfig
Analyze Angular workspace configuration (angular.json).

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `workingDirectory` (string, default: ""): Working directory
- `includeDependencyAnalysis` (bool, default: true): Include dependency analysis
- `includeArchitecturalInsights` (bool, default: true): Include architectural insights
- `includeSecurityScan` (bool, default: true): Include security vulnerability scanning

**Returns:** string - JSON with ConfigurationAnalysisResult

**Analyzes:**
- Build configurations
- Budget compliance
- Optimization flags
- Asset configuration
- Script/style bundles
- File replacements
- Source maps
- Dependency versions
- Security vulnerabilities
- Architecture patterns

## Example

```
playwright:analyze_angular_json_config \
  --workingDirectory "/path/to/project" \
  --sessionId test
```
