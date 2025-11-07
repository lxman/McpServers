# Ping Server

Tests connectivity to a MongoDB server by sending a ping command.

## Parameters

- **serverName** (string, required): The name of the server to ping

## Returns

Returns a JSON object with ping results:

```json
{
  "success": true,
  "serverName": "local",
  "responseTime": 2,
  "message": "Server is reachable"
}
```

## Example

Ping the production server:

```
serverName: "production"
```
