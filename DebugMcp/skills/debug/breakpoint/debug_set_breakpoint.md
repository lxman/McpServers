# Debug Set Breakpoint

## Description
Sets a breakpoint at a specific line in a source file. When the debugged process reaches this line, execution will pause.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |
| filePath | string | Yes | Full path to the source file |
| lineNumber | integer | Yes | Line number where the breakpoint should be set |

## Returns

Returns a JSON object with the following structure:

```json
{
  "success": "boolean",
  "breakpointId": "integer",
  "filePath": "string",
  "lineNumber": "integer",
  "verified": "boolean",
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Whether the breakpoint was successfully set |
| breakpointId | integer | Unique identifier for this breakpoint |
| filePath | string | Path to the file where breakpoint was set |
| lineNumber | integer | Line number of the breakpoint |
| verified | boolean | Whether the breakpoint was verified by the debugger |
| message | string | Status message or error description |

## Example

### Request
```json
{
  "sessionId": "session_12345",
  "filePath": "C:\\MyApp\\main.cpp",
  "lineNumber": 42
}
```

### Response
```json
{
  "success": true,
  "breakpointId": 1,
  "filePath": "C:\\MyApp\\main.cpp",
  "lineNumber": 42,
  "verified": true,
  "message": "Breakpoint set successfully"
}
```
