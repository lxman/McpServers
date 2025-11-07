# Insert One

Inserts a single document into the specified collection.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection to insert into
- **documentJson** (string, required): JSON string representing the document to insert

## Returns

Returns a JSON object with the insertion result:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "users",
  "insertedId": "507f1f77bcf86cd799439011",
  "acknowledged": true
}
```

## Example

Insert a user document:

```
serverName: "local"
collectionName: "users"
documentJson: "{\"name\": \"John Doe\", \"email\": \"john@example.com\", \"age\": 30, \"createdAt\": \"2025-01-15T10:30:00Z\"}"
```
