# Search Log Pattern

Search for a specific pattern in CloudWatch logs.

## Parameters

- **logGroupName**: Name of the log group
- **searchPattern**: Pattern to search for
- **minutesBack**: How many minutes to look back
- **limit**: Maximum events to return
- **caseSensitive**: Case sensitive search (default: false)

## Returns

Log events matching the search pattern.

## Example Usage

```json
{
  "logGroupName": "/aws/lambda/my-function",
  "searchPattern": "timeout",
  "minutesBack": 120,
  "limit": 50,
  "caseSensitive": false
}
```