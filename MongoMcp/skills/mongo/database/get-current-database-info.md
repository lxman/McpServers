# Get Current Database Info

Retrieves detailed information about the currently active database on the specified server.

## Parameters

- **serverName** (string, required): The name of the server connection

## Returns

Returns a JSON object with database information:

```json
{
  "serverName": "local",
  "databaseName": "myapp",
  "collections": 12,
  "views": 2,
  "dataSize": 8192000,
  "storageSize": 9437184,
  "indexes": 15,
  "indexSize": 229376,
  "avgObjSize": 2048
}
```

## Example

Get info for the current database:

```
serverName: "local"
```
