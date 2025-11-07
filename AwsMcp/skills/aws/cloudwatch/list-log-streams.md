# List CloudWatch Log Streams

List log streams within a CloudWatch log group.

## Parameters

- **logGroupName**: Name of the log group
- **orderBy**: Order streams by LastEventTime or LogStreamName
- **descending**: Sort in descending order (default: false)
- **limit**: Maximum streams to return

## Returns

Returns a list of log streams with their metadata.

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "orderBy": "LastEventTime",
  "descending": true,
  "limit": 50
}
```