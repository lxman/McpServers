# Delete Blob

Delete a blob from a container.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **blobName** (string): Blob name to delete

## Returns
JSON object with success status and deletion confirmation.

## Example Response
```json
{
  "success": true,
  "message": "Blob deleted successfully",
  "blobName": "file.txt",
  "containerName": "my-container"
}
```
