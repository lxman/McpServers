# Get File Share

Get details of a specific file share.

## Parameters
- **accountName** (string): Storage account name
- **shareName** (string): File share name

## Returns
JSON object with file share details.

## Example Response
```json
{
  "success": true,
  "share": {
    "name": "myshare",
    "quotaInGB": 100,
    "lastModified": "2025-01-10T12:00:00Z",
    "metadata": {}
  }
}
```
