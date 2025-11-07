# Angular Performance Tools

Monitor Angular application performance with Zone.js tracking and memory profiling.

## Methods

### MonitorZoneActivity
Monitor Zone.js activity and async operations.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds

**Returns:** string - JSON with Zone.js activity monitoring results

---

### MonitorMemoryUsage
Monitor memory usage and performance metrics.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds

**Returns:** string - JSON with memory usage analysis

---

### MonitorSystemPerformance
Monitor system-level performance metrics.

**Parameters:**
- `sessionId`, `durationSeconds`

**Returns:** string - JSON with system performance data

## Use Cases

- Zone.js async operation tracking
- Memory leak detection
- Performance bottleneck identification
- Zoneless migration planning
- Application health monitoring

## Best Practices

1. Monitor during typical user workflows
2. Look for unexpected Zone.js tasks
3. Track memory growth over time
4. Compare before/after optimization
5. Use longer durations (60s+) for memory leak detection
