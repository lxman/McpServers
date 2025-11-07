# List CloudWatch Log Groups

Lists all CloudWatch log groups in your AWS account.

## Parameters

- **maxResults**: Maximum number of log groups to return (default: 50, max: 50)
- **nextToken**: Token for pagination (optional)

## Returns

Returns a list of log groups with their names, creation times, retention policies, and sizes.

## Example Usage

```json
{
  "maxResults": 50
}
```

## Response Example

```json
{
  "success": true,
  "logGroupCount": 3,
  "logGroups": [
    {
      "logGroupName": "/aws/lambda/my-function",
      "creationTime": "2024-01-01T00:00:00Z",
      "retentionInDays": 7,
      "storedBytes": 1024000
    }
  ]
}
```