# Debug List Sessions

## Description
Lists all active debugging sessions currently running on the system.

## Parameters

None

## Returns

Returns a JSON object with the following structure:

```json
{
  "sessions": [
    {
      "sessionId": "string",
      "executablePath": "string",
      "status": "string",
      "startTime": "string"
    }
  ],
  "totalSessions": "integer"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| sessions | array | Array of session objects |
| sessions[].sessionId | string | Unique identifier for the session |
| sessions[].executablePath | string | Path to the executable being debugged |
| sessions[].status | string | Current session status (e.g., "running", "stopped", "paused") |
| sessions[].startTime | string | ISO 8601 timestamp when the session started |
| totalSessions | integer | Total number of active sessions |

## Example

### Request
```json
{}
```

### Response
```json
{
  "sessions": [
    {
      "sessionId": "session_12345",
      "executablePath": "C:\\MyApp\\MyApp.exe",
      "status": "paused",
      "startTime": "2025-11-06T15:30:45.123Z"
    },
    {
      "sessionId": "session_12346",
      "executablePath": "C:\\OtherApp\\OtherApp.exe",
      "status": "running",
      "startTime": "2025-11-06T15:25:30.456Z"
    }
  ],
  "totalSessions": 2
}
```
