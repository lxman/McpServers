# Get Container Registry

Get details of a specific container registry.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name

## Returns
JSON object with registry details.

## Example Response
```json
{
  "success": true,
  "registry": {
    "name": "myregistry",
    "resourceGroup": "my-rg",
    "location": "eastus",
    "loginServer": "myregistry.azurecr.io",
    "sku": "Basic"
  }
}
```
