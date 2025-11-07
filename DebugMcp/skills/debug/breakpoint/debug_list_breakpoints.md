# Debug List Breakpoints

## Description
Lists all breakpoints that have been set in a debugging session.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |

## Returns

Returns a JSON object with the following structure:

```json
{
  "breakpoints": [
    {
      "breakpointId": "integer",
      "filePath": "string",
      "lineNumber": "integer",
      "verified": "boolean"
    }
  ],
  "totalBreakpoints": "integer"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| breakpoints | array | Array of breakpoint objects |
| breakpoints[].breakpointId | integer | Unique identifier for the breakpoint |
| breakpoints[].filePath | string | Path to the file containing the breakpoint |
| breakpoints[].lineNumber | integer | Line number of the breakpoint |
| breakpoints[].verified | boolean | Whether the breakpoint has been verified |
| totalBreakpoints | integer | Total number of breakpoints in the session |

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
  "breakpoints": [
    {
      "breakpointId": 1,
      "filePath": "C:\\MyApp\\main.cpp",
      "lineNumber": 42,
      "verified": true
    },
    {
      "breakpointId": 2,
      "filePath": "C:\\MyApp\\utils.cpp",
      "lineNumber": 156,
      "verified": true
    },
    {
      "breakpointId": 3,
      "filePath": "C:\\MyApp\\handlers.cpp",
      "lineNumber": 78,
      "verified": false
    }
  ],
  "totalBreakpoints": 3
}
```
