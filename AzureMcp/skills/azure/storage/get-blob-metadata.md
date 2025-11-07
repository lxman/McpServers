# Get Blob Metadata

Get metadata for a specific blob.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **blobName** (string): Blob name

## Returns
JSON object with blob metadata.

## Example Response
```json
{
  "success": true,
  "blobName": "file.txt",
  "metadata": {
    "key1": "value1",
    "key2": "value2"
  }
}
```
