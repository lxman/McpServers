# Put CloudWatch Metric Data

Publish custom metrics to CloudWatch.

## Parameters

- **namespaceName**: Custom namespace for the metric
- **metricName**: Name of the metric
- **value**: Metric value
- **unit**: Unit of measurement (optional)
- **timestamp**: Timestamp for the data point (optional)

## Returns

Confirmation of metric publication.

## Example Usage

```json
{
  "namespaceName": "MyApp/Performance",
  "metricName": "RequestLatency",
  "value": 123.45,
  "unit": "Milliseconds",
  "timestamp": "2024-01-01T00:00:00Z"
}
```