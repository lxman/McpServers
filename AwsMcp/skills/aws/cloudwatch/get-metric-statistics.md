# Get CloudWatch Metric Statistics

Retrieve statistical data for a CloudWatch metric.

## Parameters

- **namespace**: Metric namespace
- **metricName**: Name of the metric
- **startTime**: Start time for statistics
- **endTime**: End time for statistics
- **period**: Period in seconds (60, 300, 900, 3600, 86400)
- **statistics**: List of statistics (Average, Sum, Minimum, Maximum, SampleCount)
- **dimensions**: Metric dimensions (optional)

## Returns

Statistical data points for the specified metric.

## Example Usage

```json
{
  "namespace": "AWS/Lambda",
  "metricName": "Duration",
  "startTime": "2024-01-01T00:00:00Z",
  "endTime": "2024-01-01T01:00:00Z",
  "period": 300,
  "statistics": ["Average", "Maximum"]
}
```