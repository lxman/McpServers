# Angular Stability Detection

Wait for Angular application to reach stability by monitoring Zone.js and async operations.

## Methods

### WaitForAngularStability
Wait for Angular application to reach stability.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `timeoutSeconds` (int, default: 30): Maximum wait time in seconds
- `checkIntervalMs` (int, default: 100): Check interval in milliseconds
- `includeDetailedInfo` (bool, default: true): Include detailed stability monitoring information

**Returns:** string - JSON with stability status

**Purpose:**
Ensures Angular has finished rendering and all async operations are complete before running tests. Critical for reliable Angular testing.

**Example:**
```
# Navigate to Angular app
playwright:navigate_to_url --url http://localhost:4200 --sessionId angular-test

# Wait for stability before testing
playwright:wait_for_angular_stability --sessionId angular-test --timeoutSeconds 30
```

---

### CheckAngularStabilityStatus
Check current Angular application stability status without waiting.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `includeDetails` (bool, default: true): Include detailed breakdown of stability checks

**Returns:** string - JSON with current stability status

**Purpose:**
Quick check to see if Angular is currently stable without blocking.

---

### MonitorZoneActivity
Monitor Zone.js activity and async operations.

**Parameters:**
- `sessionId` (string, default: "default"): Session ID
- `durationSeconds` (int, default: 30): Duration in seconds

**Returns:** string - JSON with Zone.js activity log

**Purpose:**
Debug Angular stability issues by tracking Zone.js async operations.

## When to Use

**Always use before:**
- Clicking elements
- Filling forms
- Making assertions
- Capturing screenshots

**Pattern:**
```
1. Navigate to Angular app
2. Wait for stability
3. Perform actions
4. Assert results
```

## Notes

- Essential for Angular testing reliability
- Checks Zone.js macro/micro tasks
- Monitors HTTP requests
- Tracks animation states
- Prevents flaky tests
