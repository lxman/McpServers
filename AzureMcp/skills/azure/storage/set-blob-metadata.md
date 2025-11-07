# Set Blob Metadata

Set metadata for a specific blob.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **blobName** (string): Blob name
- **metadata** (Dictionary<string, string>): Metadata key-value pairs

## Returns
JSON object with success status.

## Example Response
```json
{
  "success": true,
  "message": "Metadata updated successfully",
  "blobName": "file.txt"
}
```
