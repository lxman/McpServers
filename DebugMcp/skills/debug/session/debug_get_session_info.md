# Debug Get Session Info

## Description
Retrieves detailed information about a specific debugging session.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |

## Returns

Returns a JSON object with the following structure:

```json
{
  "sessionId": "string",
  "executablePath": "string",
  "workingDirectory": "string",
  "arguments": "string",
  "status": "string",
  "startTime": "string",
  "currentState": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| sessionId | string | Unique identifier for this session |
| executablePath | string | Path to the executable being debugged |
| workingDirectory | string | Working directory for the process |
| arguments | string | Command-line arguments passed to the process |
| status | string | Current session status |
| startTime | string | ISO 8601 timestamp when the session started |
| currentState | string | Detailed state information (e.g., "stopped at breakpoint", "running", "paused") |

## Example

### Request
```json
{
  "sessionId": "session_12345"
}
```

### Response
```json
{
  "sessionId": "session_12345",
  "executablePath": "C:\\MyApp\\MyApp.exe",
  "workingDirectory": "C:\\MyApp",
  "arguments": "--config app.json",
  "status": "paused",
  "startTime": "2025-11-06T15:30:45.123Z",
  "currentState": "stopped at breakpoint (line 42 in main.cpp)"
}
```
