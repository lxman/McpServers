# Debug Run

## Description
Starts execution of the debugged process from its current state. If the process is paused or stopped, this resumes execution.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session to run |

## Returns

Returns a JSON object with the following structure:

```json
{
  "success": "boolean",
  "sessionId": "string",
  "status": "string",
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Whether execution was successfully started |
| sessionId | string | The session ID that was run |
| status | string | Current execution status |
| message | string | Status message or error description |

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
  "success": true,
  "sessionId": "session_12345",
  "status": "running",
  "message": "Execution started successfully"
}
```
