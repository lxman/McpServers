# List Containers

List blob containers in a storage account.

## Parameters
- **accountName** (string): Storage account name
- **prefix** (string, optional): Container name prefix filter

## Returns
JSON object with success status and array of containers.

## Example Response
```json
{
  "success": true,
  "containerCount": 3,
  "containers": [
    {
      "name": "my-container",
      "lastModified": "2025-01-10T12:00:00Z",
      "publicAccess": "None",
      "leaseState": "Available",
      "leaseStatus": "Unlocked"
    }
  ]
}
```
