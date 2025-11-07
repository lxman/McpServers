# Aggregate

Executes an aggregation pipeline on the specified collection for advanced data processing and analysis.

## Parameters

- **serverName** (string, required): The name of the server connection
- **collectionName** (string, required): The name of the collection to aggregate
- **pipelineJson** (string, required): JSON string representing the aggregation pipeline stages

## Returns

Returns a JSON object with the aggregation results:

```json
{
  "serverName": "local",
  "collectionName": "orders",
  "results": [
    {
      "_id": "electronics",
      "totalSales": 125000,
      "averageOrder": 250,
      "count": 500
    },
    {
      "_id": "clothing",
      "totalSales": 85000,
      "averageOrder": 170,
      "count": 500
    }
  ],
  "count": 2
}
```

## Example

Calculate total sales by category:

```
serverName: "local"
collectionName: "orders"
pipelineJson: "[{\"$group\": {\"_id\": \"$category\", \"totalSales\": {\"$sum\": \"$amount\"}, \"averageOrder\": {\"$avg\": \"$amount\"}, \"count\": {\"$sum\": 1}}}, {\"$sort\": {\"totalSales\": -1}}]"
```

Find top customers:

```
serverName: "local"
collectionName: "orders"
pipelineJson: "[{\"$group\": {\"_id\": \"$customerId\", \"totalSpent\": {\"$sum\": \"$amount\"}, \"orderCount\": {\"$sum\": 1}}}, {\"$sort\": {\"totalSpent\": -1}}, {\"$limit\": 10}]"
```
