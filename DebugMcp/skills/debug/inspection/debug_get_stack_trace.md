# Debug Get Stack Trace

## Description
Retrieves the call stack for a debugging session. Shows the chain of function calls that led to the current execution point.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |
| threadId | integer | No | Thread ID to get stack trace for (default: 1) |

## Returns

Returns a JSON object with the following structure:

```json
{
  "frames": [
    {
      "level": "integer",
      "function": "string",
      "file": "string",
      "line": "integer",
      "source": "string"
    }
  ],
  "threadId": "integer",
  "frameCount": "integer"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| frames | array | Array of stack frame objects |
| frames[].level | integer | Stack frame depth (0 is current frame) |
| frames[].function | string | Name of the function in this frame |
| frames[].file | string | Path to the source file |
| frames[].line | integer | Line number in the source file |
| frames[].source | string | Source code line content |
| threadId | integer | Thread ID for this stack trace |
| frameCount | integer | Total number of frames in the stack |

## Example

### Request
```json
{
  "sessionId": "session_12345",
  "threadId": 1
}
```

### Response
```json
{
  "frames": [
    {
      "level": 0,
      "function": "CalculateSum",
      "file": "C:\\MyApp\\utils.cpp",
      "line": 156,
      "source": "int result = a + b;"
    },
    {
      "level": 1,
      "function": "ProcessData",
      "file": "C:\\MyApp\\main.cpp",
      "line": 42,
      "source": "sum = CalculateSum(x, y);"
    },
    {
      "level": 2,
      "function": "main",
      "file": "C:\\MyApp\\main.cpp",
      "line": 10,
      "source": "ProcessData();"
    }
  ],
  "threadId": 1,
  "frameCount": 3
}
```
