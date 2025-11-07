# Get CloudWatch Log Context

Get log events around a specific timestamp for context.

## Parameters

- **logGroupName**: Name of the log group
- **logStreamName**: Name of the log stream
- **timestamp**: Unix timestamp in milliseconds
- **contextLines**: Number of lines before/after to retrieve

## Returns

Log events around the specified timestamp.

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "logStreamName": "2024/01/01/[$LATEST]abc123",
  "timestamp": 1704067200000,
  "contextLines": 5
}
```