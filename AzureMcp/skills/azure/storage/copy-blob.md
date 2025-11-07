# Copy Blob

Copy a blob from one location to another.

## Parameters
- **sourceAccountName** (string): Source storage account name
- **sourceContainerName** (string): Source container name
- **sourceBlobName** (string): Source blob name
- **destAccountName** (string): Destination storage account name
- **destContainerName** (string): Destination container name
- **destBlobName** (string): Destination blob name

## Returns
JSON object with success status and copy details.

## Example Response
```json
{
  "success": true,
  "message": "Blob copied successfully",
  "source": {
    "accountName": "sourceaccount",
    "containerName": "source-container",
    "blobName": "source.txt"
  },
  "destination": {
    "accountName": "destaccount",
    "containerName": "dest-container",
    "blobName": "dest.txt"
  }
}
```
