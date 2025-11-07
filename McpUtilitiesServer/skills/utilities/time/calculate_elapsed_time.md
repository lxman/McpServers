# Calculate Elapsed Time

## Description
Calculates the elapsed time between two timestamps. If the end timestamp is not provided, it defaults to the current time.

## Parameters
- `startTimestamp` (string, required): Start time in ISO 8601 format
- `endTimestamp` (string, optional): End time in ISO 8601 format. Defaults to current time if not provided.

## Returns
JSON object containing:
- `startTime` (string): Start timestamp in ISO 8601 format
- `endTime` (string): End timestamp in ISO 8601 format
- `elapsedSeconds` (number): Total elapsed time in seconds
- `elapsedMilliseconds` (number): Total elapsed time in milliseconds
- `formattedElapsed` (string): Human-readable elapsed time format (e.g., "2 days, 3 hours, 15 minutes, 30 seconds")
- `isPositive` (boolean): True if end time is after start time, false otherwise

## Example JSON

### Request
```json
{
  "startTimestamp": "2024-11-04T10:00:00Z",
  "endTimestamp": "2024-11-06T13:15:30Z"
}
```

### Response
```json
{
  "startTime": "2024-11-04T10:00:00Z",
  "endTime": "2024-11-06T13:15:30Z",
  "elapsedSeconds": 179730,
  "elapsedMilliseconds": 179730000,
  "formattedElapsed": "2 days, 3 hours, 15 minutes, 30 seconds",
  "isPositive": true
}
```

### Request (with implicit current time)
```json
{
  "startTimestamp": "2024-11-06T12:00:00Z"
}
```

### Response
```json
{
  "startTime": "2024-11-06T12:00:00Z",
  "endTime": "2024-11-06T14:35:42Z",
  "elapsedSeconds": 9342,
  "elapsedMilliseconds": 9342000,
  "formattedElapsed": "2 hours, 35 minutes, 42 seconds",
  "isPositive": true
}
```
