# close_connection

Close an active database connection.

## Parameters

- **connectionName** (string, required): Connection name to close

## Returns

```json
{
  "success": true,
  "connectionName": "default",
  "message": "Connection closed"
}
```

## Example

```
close_connection("default")
```

## Use Cases

- Free resources after batch operations
- Force reconnection with new credentials
- Clean shutdown of connections

## Notes

- Connections auto-reopen on next use
- Active transactions will be lost
- Safe to call on already-closed connections
