# List Metrics

List available metrics for a resource.

## Parameters
- **resourceId** (string): Azure resource ID
- **metricNamespace** (string, optional): Metric namespace filter

## Returns
JSON object with array of available metric names.

## Example Response
```json
{
  "success": true,
  "metrics": [
    "Percentage CPU",
    "Network In",
    "Network Out",
    "Disk Read Bytes",
    "Disk Write Bytes"
  ]
}
```
