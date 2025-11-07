# Debug Continue

## Description
Continues execution of the debugged process. If paused at a breakpoint, resumes execution until the next breakpoint or program termination.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session to continue |

## Returns

Returns a JSON object with the following structure:

```json
{
  "success": "boolean",
  "sessionId": "string",
  "status": "string",
  "stoppedReason": "string",
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Whether execution was successfully continued |
| sessionId | string | The session ID that was continued |
| status | string | Current execution status |
| stoppedReason | string | Reason why execution stopped (e.g., "breakpoint hit", "exception", "program exited") |
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
  "status": "paused",
  "stoppedReason": "breakpoint hit at line 42",
  "message": "Execution continued and stopped at breakpoint"
}
```
