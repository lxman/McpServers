# Test Index

Tests an index with a sample query to verify functionality and get performance metrics. Useful for validating index quality before production use.

## Parameters

- **indexName** (string, required): Name of the index to test
- **testQuery** (string, required): Test query string to execute

## Returns

Returns a JSON object with test results:
```json
{
  "success": true,
  "indexName": "reports_2024",
  "testQuery": "sample query",
  "functional": true,
  "resultsFound": 5,
  "searchTime": 0.032,
  "averageScore": 0.78,
  "indexHealth": "good",
  "recommendations": []
}
```

## Example

```javascript
test_index({
  indexName: "reports_2024",
  testQuery: "financial report"
})
```
