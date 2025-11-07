# Get Server Status

Retrieves detailed status information about a connected MongoDB server.

## Parameters

- **serverName** (string, required): The name of the server to check status for

## Returns

Returns a JSON object with server status details:

```json
{
  "serverName": "local",
  "status": "connected",
  "databaseName": "myapp",
  "serverInfo": {
    "version": "7.0.4",
    "uptime": 86400,
    "connections": {
      "current": 5,
      "available": 838855
    }
  },
  "isDefault": true
}
```

## Example

Get status for the local server:

```
serverName: "local"
```
