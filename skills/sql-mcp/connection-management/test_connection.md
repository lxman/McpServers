# test_connection

Test if a database connection can be established.

## Parameters

- **connectionName** (string, required): Connection name from config

## Returns

```json
{
  "success": true,
  "connectionName": "default",
  "isConnected": true
}
```

## Example

```
test_connection("default")
```

## Use Cases

- Verify credentials before queries
- Health check for database
- Troubleshoot connection issues

## Error Handling

Returns `success: false` if connection fails, with error details.
