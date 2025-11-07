# Delete Container

Delete a blob container from a storage account.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name to delete

## Returns
JSON object with success status and deletion confirmation.

## Example Response
```json
{
  "success": true,
  "message": "Container deleted successfully",
  "containerName": "my-container",
  "accountName": "mystorageaccount"
}
```
