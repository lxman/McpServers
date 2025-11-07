# List Storage Accounts

List all Azure storage accounts in the subscription or specific subscription.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID to filter accounts

## Returns
JSON object with success status and array of storage accounts.

## Example Response
```json
{
  "success": true,
  "accountCount": 2,
  "accounts": [
    {
      "name": "mystorageaccount",
      "resourceGroup": "my-rg",
      "location": "eastus",
      "sku": "Standard_LRS",
      "kind": "StorageV2",
      "creationTime": "2025-01-01T00:00:00Z"
    }
  ]
}
```
