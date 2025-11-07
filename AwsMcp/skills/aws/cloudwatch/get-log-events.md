# Get CloudWatch Log Events

Retrieve log events from a specific log stream.

## Parameters

- **logGroupName**: Name of the log group
- **logStreamName**: Name of the log stream
- **startTime**: Start time for retrieval (optional)
- **endTime**: End time for retrieval (optional)
- **limit**: Maximum events to return (default: 100)
- **startFromHead**: Start from the beginning of the stream (default: false)

## Returns

Returns log events from the specified stream.

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "logStreamName": "2024/01/01/[$LATEST]abc123",
  "limit": 50
}
```

## Response Example

```json
{
  "success": true,
  "eventCount": 10,
  "events": [
    {
      "timestamp": 1704067200000,
      "message": "Processing request",
      "ingestionTime": 1704067201000
    }
  ]
}
```