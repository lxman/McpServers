# Get Error Logs from Multiple Groups

Retrieve error logs from multiple CloudWatch log groups.

## Parameters

- **logGroupNames**: List of log group names
- **minutesBack**: How many minutes to look back
- **limit**: Maximum events per group

## Returns

Error events from each log group.

## Example Usage

```json
{
  "logGroupNames": ["/aws/lambda/func1", "/aws/lambda/func2"],
  "minutesBack": 60,
  "limit": 50
}
```