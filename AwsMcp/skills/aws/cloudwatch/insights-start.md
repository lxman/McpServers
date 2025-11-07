# Start CloudWatch Insights Query

Start a CloudWatch Logs Insights query asynchronously.

## Parameters

- **logGroupNames**: Comma-separated list of log groups
- **queryString**: CloudWatch Insights query
- **startTime**: Query start time
- **endTime**: Query end time

## Returns

Query ID for retrieving results later.

## Example Usage

```json
{
  "logGroupNames": "/aws/lambda/my-function",
  "queryString": "stats count() by bin(5m)",
  "startTime": "2024-01-01T00:00:00Z",
  "endTime": "2024-01-01T01:00:00Z"
}
```