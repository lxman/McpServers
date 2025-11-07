# List Active Connections

Retrieves a list of all currently active MongoDB server connections.

## Parameters

No parameters required.

## Returns

Returns a JSON object with an array of active connections:

```json
{
  "connections": [
    {
      "serverName": "local",
      "databaseName": "myapp",
      "isDefault": true,
      "status": "connected"
    },
    {
      "serverName": "production",
      "databaseName": "prod_db",
      "isDefault": false,
      "status": "connected"
    }
  ],
  "count": 2
}
```

## Example

List all connections:

```
(no parameters)
```
