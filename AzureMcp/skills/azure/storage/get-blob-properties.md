# Get Blob Properties

Get properties of a specific blob.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **blobName** (string): Blob name

## Returns
JSON object with blob properties.

## Example Response
```json
{
  "success": true,
  "properties": {
    "contentType": "text/plain",
    "contentLength": 1024,
    "lastModified": "2025-01-10T12:00:00Z",
    "etag": "\"0x8D9...\"",
    "blobType": "BlockBlob",
    "leaseState": "Available",
    "leaseStatus": "Unlocked",
    "metadata": {}
  }
}
```
