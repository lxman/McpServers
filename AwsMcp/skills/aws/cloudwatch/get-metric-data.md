# Get CloudWatch Metric Data

Retrieve metric data using metric math expressions.

## Parameters

- **metricDataQueries**: List of metric data queries
- **startTime**: Start time for data retrieval
- **endTime**: End time for data retrieval

## Returns

Metric data results for the specified queries.

## Example Usage

```json
{
  "metricDataQueries": [
    {
      "id": "m1",
      "metricStat": {
        "metric": {
          "namespace": "AWS/Lambda",
          "metricName": "Errors",
          "dimensions": [{"name": "FunctionName", "value": "my-function"}]
        },
        "period": 300,
        "stat": "Sum"
      }
    }
  ],
  "startTime": "2024-01-01T00:00:00Z",
  "endTime": "2024-01-01T01:00:00Z"
}
```