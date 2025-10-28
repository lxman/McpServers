# Angular Zoneless Testing

Test zoneless change detection in Angular 18+ applications with migration planning and validation.

## Methods

### TestZonelessChangeDetection
Test zoneless change detection in Angular 18+ applications.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `testCycles` (int, default: 5): Number of change detection cycles to test
- `testIntervalMs` (int, default: 1000): Test interval between change detection triggers in milliseconds
- `timeoutSeconds` (int, default: 60): Maximum test duration in seconds
- `includeDetailedAnalysis` (bool, default: true): Include detailed change detection analysis

**Returns:** string - JSON with zoneless testing results

---

### ValidateChangeDetectionPatterns
Validate change detection patterns in Angular applications for zoneless compatibility.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `checkAntiPatterns` (bool, default: true): Check for common anti-patterns
- `analyzeHierarchy` (bool, default: true): Analyze component hierarchy for optimization opportunities

**Returns:** string - JSON with change detection pattern validation

---

### GenerateZonelessMigrationPlan
Generate zoneless migration recommendations for Angular applications.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `includeStepByStep` (bool, default: true): Include step-by-step migration guide
- `analyzeRisks` (bool, default: true): Analyze migration risks and blockers
- `estimateEffort` (bool, default: true): Estimate migration effort and timeline

**Returns:** string - JSON with migration plan

## Use Cases

- Zoneless Angular migration
- Change detection optimization
- Performance improvement
- Angular 18+ modernization
- Anti-pattern detection
- Migration risk assessment

## Zoneless Benefits

- **Performance**: Eliminate Zone.js overhead
- **Bundle Size**: Smaller application bundles
- **Predictability**: Explicit change detection
- **Debugging**: Easier to reason about updates
- **Modern**: Align with Angular's future direction

## Migration Considerations

1. **Signals**: Migrate to signal-based state
2. **OnPush**: Use OnPush change detection strategy
3. **Async Pipe**: Leverage async pipe for observables
4. **Manual Detection**: Use ChangeDetectorRef.markForCheck()
5. **Third-Party**: Verify library compatibility

## Best Practices

1. Start migration plan with analysis
2. Test incrementally by feature/component
3. Use validation tools to catch anti-patterns
4. Monitor performance before/after
5. Update tests for zoneless behavior
6. Consider signals for new code

## Angular Version Requirements

- Angular 18+ required for zoneless support
- Angular 17+ recommended for signals
