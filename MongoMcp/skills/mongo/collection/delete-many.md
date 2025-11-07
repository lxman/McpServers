# Delete Many

Deletes all documents that match the filter from the specified collection.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection containing the documents
- **filterJson** (string, required): JSON string representing the filter to match documents

## Returns

Returns a JSON object with the deletion results:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "users",
  "deletedCount": 15,
  "acknowledged": true
}
```

## Example

Delete all inactive users:

```
serverName: "local"
collectionName: "users"
filterJson: "{\"status\": \"inactive\"}"
```

Delete old records:

```
serverName: "local"
collectionName: "logs"
filterJson: "{\"createdAt\": {\"$lt\": \"2024-01-01T00:00:00Z\"}}"
```
