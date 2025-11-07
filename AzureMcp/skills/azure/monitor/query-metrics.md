# Query Metrics

Query metrics from Azure Monitor.

## Parameters
- **resourceId** (string): Azure resource ID
- **metricNames** (string): Comma-separated metric names
- **startTime** (string): Start time (ISO 8601)
- **endTime** (string): End time (ISO 8601)
- **intervalMinutes** (int, optional): Interval in minutes
- **aggregations** (string, optional): Comma-separated aggregations

## Returns
JSON object with metric data.

## Example Response
```json
{
  "success": true,
  "result": {
    "metrics": [
      {
        "name": "Percentage CPU",
        "unit": "Percent",
        "timeseries": [
          {
            "data": [
              {"timestamp": "2025-01-10T12:00:00Z", "average": 45.5}
            ]
          }
        ]
      }
    ]
  }
}
```
