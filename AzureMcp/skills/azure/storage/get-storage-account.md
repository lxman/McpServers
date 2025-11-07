# Get Storage Account

Get details of a specific Azure storage account.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **accountName** (string): Storage account name

## Returns
JSON object with storage account details.

## Example Response
```json
{
  "success": true,
  "account": {
    "name": "mystorageaccount",
    "resourceGroup": "my-rg",
    "location": "eastus",
    "sku": "Standard_LRS",
    "kind": "StorageV2",
    "creationTime": "2025-01-01T00:00:00Z",
    "primaryLocation": "eastus",
    "secondaryLocation": "westus"
  }
}
```
