# Set Default Server

Sets a connected server as the default for operations that don't specify a server name.

## Parameters

- **serverName** (string, required): The name of the server to set as default

## Returns

Returns a JSON object confirming the default server change:

```json
{
  "success": true,
  "defaultServer": "production",
  "message": "Default server set successfully"
}
```

## Example

Set the production server as default:

```
serverName: "production"
```
