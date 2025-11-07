# Get Container

Get properties of a specific blob container.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name

## Returns
JSON object with container properties.

## Example Response
```json
{
  "success": true,
  "container": {
    "name": "my-container",
    "lastModified": "2025-01-10T12:00:00Z",
    "publicAccess": "None",
    "leaseState": "Available",
    "leaseStatus": "Unlocked",
    "metadata": {}
  }
}
```
