# Get Connection Status

Retrieves the current status of the Redis connection, including connection state, server information, and selected database.

## Parameters

This tool takes no parameters.

## Returns

Returns a JSON object with detailed connection information:

```json
{
  "connected": true,
  "server": "localhost:6379",
  "serverVersion": "7.0.0",
  "selectedDatabase": 0,
  "uptime": "3600",
  "clientName": "RedisMcp"
}
```

If not connected:

```json
{
  "connected": false,
  "message": "Not connected to any Redis server"
}
```

## Example

Check if currently connected to Redis:
```
(no parameters required)
```

This tool is useful for:
- Verifying an active connection exists before performing operations
- Checking which database is currently selected
- Monitoring connection health
- Debugging connection issues
