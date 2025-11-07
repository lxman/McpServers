# Count Documents

Counts the number of documents in a collection that match the specified filter.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection to count documents in
- **filterJson** (string, required): JSON string representing the filter (use "{}" to count all documents)

## Returns

Returns a JSON object with the count result:

```json
{
  "serverName": "local",
  "collectionName": "users",
  "count": 1500,
  "filter": "{\"status\": \"active\"}"
}
```

## Example

Count all active users:

```
serverName: "local"
collectionName: "users"
filterJson: "{\"status\": \"active\"}"
```

Count all documents:

```
serverName: "local"
collectionName: "orders"
filterJson: "{}"
```
