# Filter CloudWatch Logs

Filter log events from a CloudWatch log group with optional pattern matching.

## Parameters

- **logGroupName**: Name of the log group to search
- **filterPattern**: CloudWatch filter pattern (optional)
  - Examples:
    - `[ERROR]` - Find logs containing ERROR
    - `?ERROR ?Exception` - Find logs with ERROR OR Exception
    - `{ $.level = "ERROR" }` - JSON logs with level=ERROR
- **startTime**: Start time for search (ISO format or Unix timestamp)
- **endTime**: End time for search (ISO format or Unix timestamp)
- **limit**: Maximum events to return (default: 100)

## Returns

Returns filtered log events matching the specified criteria.

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "filterPattern": "[ERROR]",
  "startTime": "2024-01-01T00:00:00Z",
  "endTime": "2024-01-02T00:00:00Z",
  "limit": 100
}
```

## Response Example

```json
{
  "success": true,
  "eventCount": 2,
  "events": [
    {
      "timestamp": 1704067200000,
      "message": "[ERROR] Failed to process request",
      "logStreamName": "2024/01/01/[$LATEST]abc123"
    }
  ]
}
```