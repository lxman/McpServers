# Simulate Delay

## Description
Simulates a delay by pausing execution for a specified duration. The operation can optionally include start and end timestamps for precision timing verification. Duration is clamped to a safe range of 0.1 to 30 seconds.

## Parameters
- `durationSeconds` (number, optional): Duration of the delay in seconds. Default: 5.0. Valid range: 0.1 to 30.0. Values outside this range will be clamped.
- `includeTimestamps` (boolean, optional): Whether to include start and end timestamps in the response. Default: true.

## Returns
JSON object containing:
- `requestedDuration` (number): The duration requested in seconds (before clamping)
- `actualDuration` (number): The actual duration executed in seconds (after clamping if necessary)
- `actualDurationMs` (number): The actual duration executed in milliseconds
- `startTime` (string, conditional): The start time in UTC ISO 8601 format (only if `includeTimestamps` is true)
- `endTime` (string, conditional): The end time in UTC ISO 8601 format (only if `includeTimestamps` is true)
- `precisionDifference` (number): The difference between requested and actual duration in milliseconds (usually 0-50ms)

## Example JSON

### Request (with default parameters)
```json
{
  "durationSeconds": 5.0,
  "includeTimestamps": true
}
```

### Response
```json
{
  "requestedDuration": 5.0,
  "actualDuration": 5.0,
  "actualDurationMs": 5000,
  "startTime": "2024-11-06T19:35:42.5638920Z",
  "endTime": "2024-11-06T19:35:47.5684523Z",
  "precisionDifference": 4.6
}
```

### Request (clamped to minimum)
```json
{
  "durationSeconds": 0.05,
  "includeTimestamps": true
}
```

### Response
```json
{
  "requestedDuration": 0.05,
  "actualDuration": 0.1,
  "actualDurationMs": 100,
  "startTime": "2024-11-06T19:35:42.5638920Z",
  "endTime": "2024-11-06T19:35:42.6638920Z",
  "precisionDifference": 2.3
}
```

### Request (clamped to maximum, without timestamps)
```json
{
  "durationSeconds": 45.0,
  "includeTimestamps": false
}
```

### Response
```json
{
  "requestedDuration": 45.0,
  "actualDuration": 30.0,
  "actualDurationMs": 30000,
  "precisionDifference": 3.1
}
```

## Notes
- Duration is automatically clamped to the range 0.1 to 30.0 seconds for safety
- The precision difference represents the variance between requested and actual execution time due to system scheduling
- Timestamps are in UTC ISO 8601 format for consistency
- Precision difference is typically very small (0-50ms) but may vary based on system load
