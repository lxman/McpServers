# Filter Logs from Multiple Groups

Filter log events from multiple CloudWatch log groups simultaneously.

## Parameters

- **logGroupNames**: List of log group names
- **filterPattern**: CloudWatch filter pattern (optional)
- **startTime**: Start time for search
- **endTime**: End time for search
- **limit**: Maximum events per group

## Returns

Filtered events from each log group.

## Example Usage

```json
{
  "logGroupNames": ["/aws/lambda/func1", "/aws/lambda/func2"],
  "filterPattern": "[ERROR]",
  "limit": 50
}
```