# Put CloudWatch Retention Policy

Set the retention policy for a CloudWatch log group.

## Parameters

- **logGroupName**: Name of the log group
- **retentionInDays**: Number of days to retain logs (1, 3, 5, 7, 14, 30, 60, 90, 120, 150, 180, 365, 400, 545, 731, 1827, 3653)

## Returns

Confirmation of policy update.

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "retentionInDays": 7
}
```