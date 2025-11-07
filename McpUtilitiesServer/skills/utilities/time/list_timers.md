# List Timers

## Description
Lists all currently active timers with their names, start times, and current elapsed times. Displays a summary of active timers and the current system time.

## Parameters
None

## Returns
JSON object containing:
- `timers` (array): Array of active timer objects, each containing:
  - `timerName` (string): The name of the timer
  - `startTime` (string): The start time in UTC ISO 8601 format
  - `elapsedSeconds` (number): Current elapsed time in seconds since start
  - `elapsedMilliseconds` (number): Current elapsed time in milliseconds since start
- `totalTimers` (number): Total count of active timers
- `currentTime` (string): Current system time in UTC ISO 8601 format

## Example JSON

### Request
```json
{}
```

### Response (with active timers)
```json
{
  "timers": [
    {
      "timerName": "build_process",
      "startTime": "2024-11-06T19:35:42.5638920Z",
      "elapsedSeconds": 342,
      "elapsedMilliseconds": 342156
    },
    {
      "timerName": "database_migration",
      "startTime": "2024-11-06T19:40:15.1234567Z",
      "elapsedSeconds": 89,
      "elapsedMilliseconds": 89450
    },
    {
      "timerName": "integration_test",
      "startTime": "2024-11-06T19:25:10.9876543Z",
      "elapsedSeconds": 1137,
      "elapsedMilliseconds": 1137234
    }
  ],
  "totalTimers": 3,
  "currentTime": "2024-11-06T19:41:24.7198765Z"
}
```

### Response (no active timers)
```json
{
  "timers": [],
  "totalTimers": 0,
  "currentTime": "2024-11-06T19:41:24.7198765Z"
}
```

## Notes
- Timers are displayed in the order they appear in the environment variables
- The elapsed times are calculated relative to the current system time
- Elapsed times update in real-time with each request
