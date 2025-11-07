# Download Blob

Download blob content as text.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **blobName** (string): Blob name

## Returns
JSON object with blob content.

## Example Response
```json
{
  "success": true,
  "blobName": "file.txt",
  "containerName": "my-container",
  "content": "Hello, World!"
}
```
