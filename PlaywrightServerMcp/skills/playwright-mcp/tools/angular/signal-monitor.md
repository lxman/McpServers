# Angular Signal Monitor

Monitor Angular signals in real-time, tracking signal changes and dependencies (Angular 17+).

## Methods

### MonitorSignalUpdates
Monitor Angular signals in real-time, tracking signal changes and dependencies.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds to monitor signals
- `maxSignals` (int, default: 50): Maximum number of signals to track simultaneously

**Returns:** string - JSON with signal monitoring results

---

### AnalyzeSignalDependencies
Analyze signal dependencies and detect circular dependencies.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with signal dependency analysis

## Use Cases

- Signal-based state tracking
- Dependency graph analysis
- Circular dependency detection
- Signal performance monitoring
- Migration from RxJS to signals
- Computed signal optimization

## Best Practices

1. Monitor during user interactions for reactive updates
2. Track signal dependency chains
3. Look for circular dependencies
4. Optimize computed signals based on findings
5. Validate signal update patterns
6. Use for zoneless migration planning

## Angular Version Requirements

- Angular 17+ (Signals are a new feature)
- Zoneless applications benefit most from signal monitoring
