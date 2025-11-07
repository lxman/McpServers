# Update One

Updates a single document that matches the filter in the specified collection.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection containing the document
- **filterJson** (string, required): JSON string representing the filter to match the document
- **updateJson** (string, required): JSON string representing the update operations (must use update operators like $set, $inc, etc.)

## Returns

Returns a JSON object with the update result:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "users",
  "matchedCount": 1,
  "modifiedCount": 1,
  "acknowledged": true
}
```

## Example

Update a user's age:

```
serverName: "local"
collectionName: "users"
filterJson: "{\"email\": \"john@example.com\"}"
updateJson: "{\"$set\": {\"age\": 31, \"updatedAt\": \"2025-01-15T10:30:00Z\"}}"
```

Increment a counter:

```
serverName: "local"
collectionName: "analytics"
filterJson: "{\"page\": \"/home\"}"
updateJson: "{\"$inc\": {\"views\": 1}}"
```
