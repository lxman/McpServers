# Start Timer

## Description
Starts a named timer that stores the start time. If a timer with the same name already exists, it will be overwritten. Timers are stored in environment variables with the key pattern `timer_{timerName}`.

## Parameters
- `timerName` (string, required): The name of the timer to start

## Returns
JSON object containing:
- `timerName` (string): The name of the timer that was started
- `startTime` (string): The start time in UTC ISO 8601 format
- `unixMilliseconds` (number): Unix timestamp in milliseconds when timer started
- `message` (string): Confirmation message indicating timer started
- `overwrittenExisting` (boolean): True if an existing timer with the same name was overwritten, false if this is a new timer

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
  "unixMilliseconds": 1730895342563,
  "message": "Timer 'build_process' started",
  "overwrittenExisting": false
}
```

### Request (overwriting existing timer)
```json
{
  "timerName": "build_process"
}
```

### Response
```json
{
  "timerName": "build_process",
  "startTime": "2024-11-06T19:45:15.1234567Z",
  "unixMilliseconds": 1730895915123,
  "message": "Timer 'build_process' started",
  "overwrittenExisting": true
}
```

## Notes
- Timers are stored in environment variables with the pattern `timer_{timerName}`
- Timer names are case-sensitive
- Existing timers with the same name will be replaced without warning
