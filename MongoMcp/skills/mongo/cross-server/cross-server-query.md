# Cross Server Query

Executes the same query across multiple servers and aggregates the results.

## Parameters

- **serverNames** (array of strings, required): Array of server names to query
- **collectionName** (string, required): The name of the collection to query on each server
- **filterJson** (string, required): JSON string representing the query filter
- **limitPerServer** (number, required): Maximum number of documents to return from each server

## Returns

Returns a JSON object with aggregated query results:

```json
{
  "collectionName": "users",
  "results": [
    {
      "serverName": "east-region",
      "count": 10,
      "documents": [
        {
          "_id": "507f1f77bcf86cd799439011",
          "name": "John Doe",
          "region": "east"
        }
      ]
    },
    {
      "serverName": "west-region",
      "count": 10,
      "documents": [
        {
          "_id": "507f1f77bcf86cd799439012",
          "name": "Jane Smith",
          "region": "west"
        }
      ]
    }
  ],
  "totalDocuments": 20,
  "serversQueried": 2
}
```

## Example

Query active users across all regional servers:

```
serverNames: ["east-region", "west-region", "central-region"]
collectionName: "users"
filterJson: "{\"status\": \"active\"}"
limitPerServer: 100
```
