# Update Many

Updates all documents that match the filter in the specified collection.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection containing the documents
- **filterJson** (string, required): JSON string representing the filter to match documents
- **updateJson** (string, required): JSON string representing the update operations (must use update operators like $set, $inc, etc.)

## Returns

Returns a JSON object with the update results:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "users",
  "matchedCount": 15,
  "modifiedCount": 15,
  "acknowledged": true
}
```

## Example

Update all users with a specific status:

```
serverName: "local"
collectionName: "users"
filterJson: "{\"status\": \"pending\"}"
updateJson: "{\"$set\": {\"status\": \"active\", \"activatedAt\": \"2025-01-15T10:30:00Z\"}}"
```

Add a field to all documents:

```
serverName: "local"
collectionName: "products"
filterJson: "{}"
updateJson: "{\"$set\": {\"version\": 2}}"
```
