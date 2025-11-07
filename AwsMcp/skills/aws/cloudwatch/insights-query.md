# Run CloudWatch Insights Query

Execute a CloudWatch Logs Insights query and wait for results.

## Parameters

- **logGroupNames**: Comma-separated list of log groups
- **queryString**: CloudWatch Insights query language query
- **startTime**: Query start time
- **endTime**: Query end time

## Returns

Query results after completion (waits up to 30 seconds).

## Example Usage

```json
{
  "logGroupNames": "/aws/lambda/func1,/aws/lambda/func2",
  "queryString": "fields @timestamp, @message | filter @message like /ERROR/ | limit 20",
  "startTime": "2024-01-01T00:00:00Z",
  "endTime": "2024-01-02T00:00:00Z"
}
```