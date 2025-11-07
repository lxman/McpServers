# Debug Stop

## Description
Terminates an active debugging session. Stops the debugger and detaches from the target process.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session to stop |

## Returns

Returns a JSON object with the following structure:

```json
{
  "success": "boolean",
  "sessionId": "string",
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Whether the session was successfully stopped |
| sessionId | string | The session ID that was stopped |
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
  "message": "Debug session stopped successfully"
}
```
