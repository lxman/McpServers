# Get Error Logs

Retrieve error logs from a CloudWatch log group using common error patterns.

## Parameters

- **logGroupName**: Name of the log group
- **minutesBack**: How many minutes to look back
- **limit**: Maximum events to return

## Returns

Log events matching common error patterns (ERROR, EXCEPTION, FAIL, FATAL, CRITICAL).

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "minutesBack": 60,
  "limit": 100
}
```