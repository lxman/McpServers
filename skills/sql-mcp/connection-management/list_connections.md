# list_connections

List all available database connections from configuration.

## Parameters

None

## Returns

```json
{
  "success": true,
  "connections": ["default", "sqlite-local", "custom-db"]
}
```

## Example

```
list_connections()
```

Returns all connection names defined in appsettings.json.

## Use Cases

- Discover available databases
- Verify configuration loaded
- Select connection for queries
