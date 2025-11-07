# Debug Get Threads

## Description
Retrieves information about all active threads in the debugging session.

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| sessionId | string | Yes | Unique identifier of the session |

## Returns

Returns a JSON object with the following structure:

```json
{
  "threads": [
    {
      "threadId": "integer",
      "name": "string",
      "state": "string",
      "location": "string"
    }
  ],
  "threadCount": "integer"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| threads | array | Array of thread objects |
| threads[].threadId | integer | Unique identifier for the thread |
| threads[].name | string | Display name of the thread |
| threads[].state | string | Current state (e.g., "running", "stopped", "waiting") |
| threads[].location | string | Current location in code (file:line or function name) |
| threadCount | integer | Total number of active threads |

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
  "threads": [
    {
      "threadId": 1,
      "name": "Main Thread",
      "state": "paused",
      "location": "main.cpp:42"
    },
    {
      "threadId": 2,
      "name": "Worker Thread 1",
      "state": "running",
      "location": "worker.cpp:89"
    },
    {
      "threadId": 3,
      "name": "Worker Thread 2",
      "state": "waiting",
      "location": "worker.cpp:105 (mutex)"
    }
  ],
  "threadCount": 3
}
```
