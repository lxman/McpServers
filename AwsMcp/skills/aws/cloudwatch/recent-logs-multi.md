# Get Recent Logs from Multiple Groups

Retrieve recent logs from multiple CloudWatch log groups.

## Parameters

- **logGroupNames**: List of log group names
- **minutesBack**: How many minutes to look back
- **limit**: Maximum events per group

## Returns

Recent events from each log group.

## Example Usage

```json
{
  "logGroupNames": ["/aws/lambda/func1", "/aws/lambda/func2"],
  "minutesBack": 30,
  "limit": 100
}
```