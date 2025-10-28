# Angular Lifecycle Monitor

Monitor Angular component lifecycle hooks with timing and performance analysis.

## Methods

### TraceComponentLifecycleHooks
Monitor Angular component lifecycle hooks execution.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds to monitor
- `maxComponents` (int, default: 25): Maximum number of components to monitor
- `includeTimingDetails` (bool, default: true): Include hook execution timing
- `specificHooks` (string?, optional): Monitor specific hooks (comma-separated: ngOnInit,ngOnDestroy,ngOnChanges,ngAfterViewInit,ngAfterViewChecked,ngAfterContentInit,ngAfterContentChecked,ngDoCheck)

**Returns:** string - JSON with lifecycle hook execution data

---

### CheckComponentLifecycleStatus
Get quick lifecycle monitoring status without full monitoring.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID

**Returns:** string - JSON with lifecycle status

---

### ProfileComponentRenderTimes
Profile individual component rendering performance.

**Parameters:**
- `sessionId`, `durationSeconds`, `maxComponents`, `includeDetailedAnalysis`

**Returns:** string - JSON with render timing analysis

---

### AnalyzeLifecycleHookPatterns
Monitor specific lifecycle hook execution patterns.

**Parameters:**
- `hookName` (string, required): Specific lifecycle hook (ngOnInit, ngOnDestroy, ngDoCheck, etc.)
- `sessionId`, `durationSeconds`

**Returns:** string - JSON with hook pattern analysis
