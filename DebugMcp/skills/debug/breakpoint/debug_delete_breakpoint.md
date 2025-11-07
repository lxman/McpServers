# Debug Delete Breakpoint

## Description
Removes a previously set breakpoint from a debugging session.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |
| breakpointId | integer | Yes | Unique identifier of the breakpoint to delete |

## Returns

Returns a JSON object with the following structure:

```json
{
  "success": "boolean",
  "breakpointId": "integer",
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Whether the breakpoint was successfully deleted |
| breakpointId | integer | The breakpoint ID that was deleted |
| message | string | Status message or error description |

## Example

### Request
```json
{
  "sessionId": "session_12345",
  "breakpointId": 2
}
```

### Response
```json
{
  "success": true,
  "breakpointId": 2,
  "message": "Breakpoint deleted successfully"
}
```
