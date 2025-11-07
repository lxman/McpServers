# Delete One

Deletes a single document that matches the filter from the specified collection.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection containing the document
- **filterJson** (string, required): JSON string representing the filter to match the document

## Returns

Returns a JSON object with the deletion result:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "users",
  "deletedCount": 1,
  "acknowledged": true
}
```

## Example

Delete a user by email:

```
serverName: "local"
collectionName: "users"
filterJson: "{\"email\": \"john@example.com\"}"
```

Delete by ID:

```
serverName: "local"
collectionName: "orders"
filterJson: "{\"_id\": \"507f1f77bcf86cd799439011\"}"
```
