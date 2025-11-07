# Stop Timer

## Description
Stops a named timer and calculates the elapsed time since it was started. The timer is automatically cleared from environment variables after stopping. If the timer does not exist, an error will be returned.

## Parameters
- `timerName` (string, required): The name of the timer to stop

## Returns
JSON object containing:
- `timerName` (string): The name of the timer that was stopped
- `startTime` (string): The start time in UTC ISO 8601 format
- `endTime` (string): The end time in UTC ISO 8601 format
- `elapsedSeconds` (number): Total elapsed time in seconds
- `elapsedMilliseconds` (number): Total elapsed time in milliseconds
- `formattedElapsed` (string): Human-readable elapsed time format (e.g., "1 hour, 23 minutes, 45 seconds")
- `message` (string): Confirmation message indicating timer stopped

## Example JSON

### Request
```json
{
  "timerName": "build_process"
}
```

### Response
```json
{
  "timerName": "build_process",
  "startTime": "2024-11-06T19:35:42.5638920Z",
  "endTime": "2024-11-06T21:00:28.1234567Z",
  "elapsedSeconds": 5465,
  "elapsedMilliseconds": 5465596,
  "formattedElapsed": "1 hour, 31 minutes, 5 seconds",
  "message": "Timer 'build_process' stopped after 1 hour, 31 minutes, 5 seconds"
}
```

### Request (timer not found)
```json
{
  "timerName": "nonexistent_timer"
}
```

### Response
```json
{
  "error": "Timer 'nonexistent_timer' not found. No active timer with this name.",
  "timerName": "nonexistent_timer",
  "message": "Timer stop failed"
}
```

## Notes
- Timer names are case-sensitive
- Stopping a timer automatically removes it from environment variables
- Attempting to stop a timer that doesn't exist will result in an error
