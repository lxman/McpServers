# List File Shares

List file shares in a storage account.

## Parameters
- **accountName** (string): Storage account name
- **prefix** (string, optional): Share name prefix filter

## Returns
JSON object with success status and array of file shares.

## Example Response
```json
{
  "success": true,
  "shares": [
    {
      "name": "myshare",
      "quotaInGB": 100,
      "lastModified": "2025-01-10T12:00:00Z"
    }
  ]
}
```
