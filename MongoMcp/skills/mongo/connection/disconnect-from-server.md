# Disconnect from Server

Closes the connection to a MongoDB server instance and releases associated resources.

## Parameters

- **serverName** (string, required): The name of the server connection to close

## Returns

Returns a JSON object confirming disconnection:

```json
{
  "success": true,
  "serverName": "myserver",
  "message": "Successfully disconnected from server"
}
```

## Example

Disconnect from a server:

```
serverName: "local"
```
