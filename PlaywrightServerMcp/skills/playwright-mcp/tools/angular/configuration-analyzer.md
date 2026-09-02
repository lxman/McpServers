# Angular Configuration Analyzer

Analyze angular.json configuration with validation and best practices.

## Methods

### AnalyzeAngularJsonConfig
Analyze Angular workspace configuration (angular.json).

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `workingDirectory` (string, **required**): Absolute path to the Angular project root. No default -- see [Working directory](#working-directory).
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

## Working directory

`workingDirectory` is **required** on every tool here, and must be an **absolute** path to your
Angular project root.

It used to fall back to the process's current directory. This server now runs as a shared HTTP
backend out of a versioned deploy directory, so that fallback silently aimed these tools at the
server's own install rather than at your project -- reporting on it, building in it, and in the
case of `ng generate` writing into it. A blank, relative, or non-existent path is now refused with
a message saying what to pass instead.
