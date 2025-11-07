# Debug Step Over

## Description
Executes the current line and pauses at the next line. If the current line contains a function call, the function is executed completely without stepping through its internals.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session to step over |

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
  "currentLine": 43,
  "currentFile": "C:\\MyApp\\main.cpp",
  "message": "Stepped over function call successfully"
}
```
