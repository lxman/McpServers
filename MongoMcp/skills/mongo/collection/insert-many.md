# Insert Many

Inserts multiple documents into the specified collection in a single operation.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection to insert into
- **documentsJson** (string, required): JSON string representing an array of documents to insert

## Returns

Returns a JSON object with the insertion results:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "users",
  "insertedCount": 3,
  "insertedIds": [
    "507f1f77bcf86cd799439011",
    "507f1f77bcf86cd799439012",
    "507f1f77bcf86cd799439013"
  ],
  "acknowledged": true
}
```

## Example

Insert multiple user documents:

```
serverName: "local"
collectionName: "users"
documentsJson: "[{\"name\": \"Alice\", \"email\": \"alice@example.com\", \"age\": 25}, {\"name\": \"Bob\", \"email\": \"bob@example.com\", \"age\": 35}, {\"name\": \"Charlie\", \"email\": \"charlie@example.com\", \"age\": 28}]"
```
