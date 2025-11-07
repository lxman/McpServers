# Query

Queries documents from the specified collection with optional filtering, limiting, and skipping.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection to query
- **filterJson** (string, required): JSON string representing the query filter (use "{}" for all documents)
- **limit** (number, optional): Maximum number of documents to return (default: 100)
- **skip** (number, optional): Number of documents to skip (default: 0)

## Returns

Returns a JSON object with the query results:

```json
{
  "serverName": "local",
  "collectionName": "users",
  "documents": [
    {
      "_id": "507f1f77bcf86cd799439011",
      "name": "John Doe",
      "email": "john@example.com",
      "age": 30
    }
  ],
  "count": 1,
  "limit": 100,
  "skip": 0
}
```

## Example

Query all users over age 25:

```
serverName: "local"
collectionName: "users"
filterJson: "{\"age\": {\"$gt\": 25}}"
limit: 10
skip: 0
```

Query all documents:

```
serverName: "local"
collectionName: "users"
filterJson: "{}"
```
