# Create Index

Creates an index on the specified collection to improve query performance.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection to create the index on
- **indexJson** (string, required): JSON string representing the index specification (e.g., {"email": 1} for ascending, {"age": -1} for descending)
- **indexName** (string, required): A unique name for the index

## Returns

Returns a JSON object with the index creation result:

```json
{
  "success": true,
  "serverName": "local",
  "collectionName": "users",
  "indexName": "email_unique",
  "message": "Index created successfully"
}
```

## Example

Create a unique index on email:

```
serverName: "local"
collectionName: "users"
indexJson: "{\"email\": 1}"
indexName: "email_unique"
```

Create a compound index:

```
serverName: "local"
collectionName: "orders"
indexJson: "{\"customerId\": 1, \"createdAt\": -1}"
indexName: "customer_date_idx"
```

Create a text index:

```
serverName: "local"
collectionName: "articles"
indexJson: "{\"title\": \"text\", \"content\": \"text\"}"
indexName: "text_search_idx"
```
