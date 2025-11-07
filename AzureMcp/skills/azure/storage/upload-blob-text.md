# Upload Blob

Upload text content to a blob.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **blobName** (string): Blob name
- **content** (string): Content to upload
- **contentType** (string, optional): Content type

## Returns
JSON object with success status and upload confirmation.

## Example Response
```json
{
  "success": true,
  "message": "Blob uploaded successfully",
  "blobName": "file.txt",
  "containerName": "my-container"
}
```
