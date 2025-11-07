# Performance Testing Tools

Core Web Vitals measurement, Lighthouse audits, code coverage, and performance profiling.

## Methods

### GetPerformanceMetrics
Get performance metrics (Core Web Vitals, load times).

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with performance metrics including LCP, FID, CLS, TTFB

---

### RunLighthouseAudit
Run Lighthouse performance audits.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `category` (string?, optional): Optional audit category - performance, accessibility, best-practices, seo, or 'all'

**Returns:** string - JSON with Lighthouse scores and recommendations

---

### StartPerformanceTrace
Start performance tracing.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - Success message

---

### StopPerformanceTrace
Stop performance tracing and return results.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with performance trace data

---

### StartCoverageTracking
Start JavaScript and CSS coverage tracking.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `js` (bool, default: true): Enable JavaScript coverage
- `css` (bool, default: true): Enable CSS coverage

**Returns:** string - Success message

---

### StopCoverageTracking
Stop coverage tracking and return results.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with coverage report (files, usage percentages)

---

### MonitorMemoryUsage
Monitor memory usage and performance metrics.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds

**Returns:** string - JSON with memory usage over time

---

### MonitorSystemPerformance
Monitor memory usage and performance.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds

**Returns:** string - JSON with system performance metrics

## Core Web Vitals

- **LCP (Largest Contentful Paint)**: < 2.5s (good)
- **FID (First Input Delay)**: < 100ms (good)
- **CLS (Cumulative Layout Shift)**: < 0.1 (good)
- **TTFB (Time to First Byte)**: < 800ms (good)

## Coverage Analysis

Identifies unused JavaScript and CSS:
- Shows % of code actually executed
- Highlights dead code for removal
- Improves bundle size optimization

## Testing Pattern

```
# 1. Start monitoring
playwright:start_performance_trace --sessionId test
playwright:start_coverage_tracking --sessionId test

# 2. Perform actions
playwright:navigate_to_url --url https://example.com
# ... user interactions ...

# 3. Get results
playwright:stop_performance_trace --sessionId test
playwright:stop_coverage_tracking --sessionId test
playwright:get_performance_metrics --sessionId test
```

## Notes

- Run Lighthouse for comprehensive audits
- Track coverage to identify unused code
- Monitor memory for leak detection
- Core Web Vitals critical for user experience
