# List CloudWatch Metrics

List available CloudWatch metrics with optional filtering.

## Parameters

- **namespace**: Filter by metric namespace (optional)
- **metricName**: Filter by metric name (optional)
- **dimensions**: Filter by dimensions (optional)

## Returns

List of available metrics matching the criteria.

## Example Usage

```json
{
  "namespace": "AWS/Lambda",
  "metricName": "Errors"
}
```