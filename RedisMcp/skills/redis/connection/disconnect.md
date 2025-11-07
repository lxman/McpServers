# Disconnect

Closes the current connection to the Redis server and releases any associated resources.

## Parameters

This tool takes no parameters.

## Returns

Returns a JSON object confirming the disconnection:

```json
{
  "success": true,
  "message": "Successfully disconnected from Redis"
}
```

If no connection was active:

```json
{
  "success": false,
  "message": "No active connection to disconnect"
}
```

## Example

Simply invoke the disconnect tool to close the current Redis connection:
```
(no parameters required)
```

This is useful when you want to:
- Clean up resources after completing Redis operations
- Switch to a different Redis server
- Ensure connections are properly closed before application shutdown
