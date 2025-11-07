# Get Recent CloudWatch Logs

Retrieve recent log events from a CloudWatch log group.

## Parameters

- **logGroupName**: Name of the log group
- **minutesBack**: Number of minutes to look back (default: 60)
- **limit**: Maximum events to return (default: 100)

## Returns

Returns recent log events from the specified time period.

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "minutesBack": 30,
  "limit": 50
}
```

## Response Example

```json
{
  "success": true,
  "eventCount": 25,
  "events": [
    {
      "timestamp": 1704067200000,
      "message": "Function execution started",
      "logStreamName": "2024/01/01/[$LATEST]abc123"
    }
  ]
}
```