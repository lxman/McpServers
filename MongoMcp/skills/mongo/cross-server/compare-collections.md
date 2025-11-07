# Compare Collections

Compares the contents of a collection across two different servers to identify differences.

## Parameters

- **server1** (string, required): The name of the first server connection
- **server2** (string, required): The name of the second server connection
- **collectionName** (string, required): The name of the collection to compare
- **filterJson** (string, optional): JSON string representing a filter to limit comparison scope (default: "{}")

## Returns

Returns a JSON object with comparison results:

```json
{
  "server1": "staging",
  "server2": "production",
  "collectionName": "users",
  "comparison": {
    "server1Count": 1500,
    "server2Count": 1485,
    "onlyInServer1": 20,
    "onlyInServer2": 5,
    "different": 12,
    "identical": 1468
  },
  "sampleDifferences": [
    {
      "_id": "507f1f77bcf86cd799439011",
      "field": "status",
      "server1Value": "active",
      "server2Value": "pending"
    }
  ]
}
```

## Example

Compare user collections between staging and production:

```
server1: "staging"
server2: "production"
collectionName: "users"
filterJson: "{}"
```

Compare only active users:

```
server1: "staging"
server2: "production"
collectionName: "users"
filterJson: "{\"status\": \"active\"}"
```
