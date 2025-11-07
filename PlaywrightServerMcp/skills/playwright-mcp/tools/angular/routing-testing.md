# Angular Routing Testing

Test Angular routing scenarios with navigation testing and guard/resolver validation.

## Methods

### TestAngularRoutingScenarios
Test Angular routing scenarios with comprehensive validation.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `testNavigationGuards` (bool, default: true): Test navigation guards (canActivate, canDeactivate, etc.)
- `testResolvers` (bool, default: true): Test route resolvers and data loading
- `testLazyLoading` (bool, default: true): Test lazy loading modules and route loading
- `includeDetailedAnalysis` (bool, default: true): Include detailed route analysis
- `timeoutSeconds` (int, default: 60): Maximum test execution time in seconds

**Returns:** string - JSON with routing test results

---

### AnalyzeAngularRoutingConfiguration
Analyze Angular routing configuration and extract route information.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `includeRouteTree` (bool, default: true): Include detailed route tree structure
- `includeGuardAnalysis` (bool, default: true): Include guard and resolver analysis

**Returns:** string - JSON with routing configuration analysis

---

### TestNavigationGuardBehaviors
Test specific navigation guard behaviors and validate guard execution.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `routesToTest` (string, default: ""): Routes to test guards on (comma-separated)
- `includeDetailedAnalysis` (bool, default: true): Include detailed guard execution analysis
- `timeoutSeconds` (int, default: 30): Maximum test execution time in seconds

**Returns:** string - JSON with guard behavior analysis

## Use Cases

- Route navigation testing
- Guard execution validation
- Lazy loading verification
- Resolver data loading testing
- Route tree analysis
- Navigation flow debugging

## Best Practices

1. Test all guard types (canActivate, canDeactivate, canLoad, canActivateChild)
2. Verify lazy loading chunks load correctly
3. Test resolver data availability
4. Validate route parameter handling
5. Test navigation error handling
6. Verify redirect logic
