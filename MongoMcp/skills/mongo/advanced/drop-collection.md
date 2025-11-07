# Drop Collection

Permanently deletes a collection and all its documents from the database.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection to drop

## Returns

Returns a JSON object confirming the collection deletion:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "temp_data",
  "message": "Collection dropped successfully"
}
```

## Example

Drop a temporary collection:

```
serverName: "local"
collectionName: "temp_data"
```

Warning: This operation is permanent and cannot be undone. All data in the collection will be lost.
