# List Log Streams

List log streams (tables) in a workspace.

## Parameters
- **workspaceId** (string): Log Analytics workspace ID

## Returns
JSON object with array of stream names.

## Example Response
```json
{
  "success": true,
  "streams": [
    "AppTraces",
    "ContainerLog",
    "Syslog"
  ]
}
```
