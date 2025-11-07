# Create File Share

Create a new file share in a storage account.

## Parameters
- **accountName** (string): Storage account name
- **shareName** (string): File share name to create
- **quotaInGB** (int, optional): Share quota in GB
- **metadata** (Dictionary<string, string>, optional): Metadata key-value pairs

## Returns
JSON object with success status and share details.

## Example Response
```json
{
  "success": true,
  "share": {
    "name": "myshare",
    "quotaInGB": 100,
    "lastModified": "2025-01-10T12:00:00Z"
  }
}
```
