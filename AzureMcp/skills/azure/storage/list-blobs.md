# List Blobs

List blobs in a container.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **prefix** (string, optional): Blob name prefix filter
- **maxResults** (int, optional): Maximum number of results to return

## Returns
JSON object with success status and array of blobs.

## Example Response
```json
{
  "success": true,
  "blobCount": 5,
  "blobs": [
    {
      "name": "file.txt",
      "contentType": "text/plain",
      "contentLength": 1024,
      "lastModified": "2025-01-10T12:00:00Z",
      "blobType": "BlockBlob",
      "leaseState": "Available"
    }
  ]
}
```
