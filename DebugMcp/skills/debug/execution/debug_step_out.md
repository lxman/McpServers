# Debug Step Out

## Description
Executes the remainder of the current function and pauses at the first line of the calling function (or at program exit if in main).

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session to step out |

## Returns

Returns a JSON object with the following structure:

```json
{
  "success": "boolean",
  "sessionId": "string",
  "currentLine": "integer",
  "currentFile": "string",
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Whether the step operation was successful |
| sessionId | string | The session ID that was stepped |
| currentLine | integer | Line number where execution is now paused |
| currentFile | string | File path where execution is paused |
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
  "currentLine": 45,
  "currentFile": "C:\\MyApp\\main.cpp",
  "message": "Stepped out of function successfully"
}
```
