# Delete CloudWatch Log Group

Delete a CloudWatch log group and all its log streams.

## Parameters

- **logGroupName**: Name of the log group to delete

## Returns

Confirmation of deletion.

## Example Usage

```json
{
  "logGroupName": "/my-app/old-logs"
}
```

## Warning

This action is irreversible and will delete all log streams and events within the group.