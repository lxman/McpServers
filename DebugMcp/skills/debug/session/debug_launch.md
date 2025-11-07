# Debug Launch

## Description
Launches a new debugging session for the specified executable. Initializes the debugger and attaches it to the target process.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| executablePath | string | Yes | Full path to the executable to debug |
| workingDirectory | string | No | Working directory for the debugged process |
| arguments | string | No | Command-line arguments to pass to the executable |

## Returns

Returns a JSON object with the following structure:

```json
{
  "sessionId": "string",
  "executablePath": "string",
  "workingDirectory": "string",
  "arguments": "string",
  "status": "string",
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| sessionId | string | Unique identifier for this debug session |
| executablePath | string | Path to the executable being debugged |
| workingDirectory | string | Working directory for the process |
| arguments | string | Command-line arguments passed to the process |
| status | string | Current session status (e.g., "running", "stopped", "paused") |
| message | string | Status message or error description |

## Example

### Request
```json
{
  "executablePath": "C:\\MyApp\\MyApp.exe",
  "workingDirectory": "C:\\MyApp",
  "arguments": "--config app.json --verbose"
}
```

### Response
```json
{
  "sessionId": "session_12345",
  "executablePath": "C:\\MyApp\\MyApp.exe",
  "workingDirectory": "C:\\MyApp",
  "arguments": "--config app.json --verbose",
  "status": "running",
  "message": "Debugging session started successfully"
}
```
