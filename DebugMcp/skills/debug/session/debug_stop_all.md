# Debug Stop All

## Description
Terminates all active debugging sessions. Stops all debuggers and detaches from all target processes.

## Parameters

None

## Returns

Returns a JSON object with the following structure:

```json
{
  "stoppedCount": "integer",
  "stoppedSessions": [
    {
      "sessionId": "string",
      "status": "string"
    }
  ],
  "message": "string"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| stoppedCount | integer | Number of sessions that were stopped |
| stoppedSessions | array | Array of stopped session objects |
| stoppedSessions[].sessionId | string | Unique identifier of the stopped session |
| stoppedSessions[].status | string | Final status of the session |
| message | string | Status message |

## Example

### Request
```json
{}
```

### Response
```json
{
  "stoppedCount": 2,
  "stoppedSessions": [
    {
      "sessionId": "session_12345",
      "status": "stopped"
    },
    {
      "sessionId": "session_12346",
      "status": "stopped"
    }
  ],
  "message": "All debug sessions stopped successfully"
}
```
