# Debug Step Into

## Description
Executes the current line and enters into any function calls. Pauses at the first line within the called function.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session to step into |

## Returns

Returns a JSON object with the following structure:

```json
{
  "success": "boolean",
  "sessionId": "string",
  "currentLine": "integer",
  "currentFile": "string",
  "functionName": "string",
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
| functionName | string | Name of the function that was entered |
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
  "currentLine": 156,
  "currentFile": "C:\\MyApp\\utils.cpp",
  "functionName": "CalculateSum",
  "message": "Stepped into function successfully"
}
```
