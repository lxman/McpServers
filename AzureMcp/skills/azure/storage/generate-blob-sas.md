# Generate Blob SAS

Generate a Shared Access Signature (SAS) URL for a blob.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **blobName** (string): Blob name
- **expiryHours** (int, optional): Expiry time in hours (default: 1)
- **permissions** (string, optional): Permissions string (default: "r")

## Returns
JSON object with SAS URL.

## Example Response
```json
{
  "success": true,
  "sasUrl": "https://account.blob.core.windows.net/container/blob?sv=2021-06-08&se=...",
  "expiresIn": "1 hours",
  "permissions": "r"
}
```
