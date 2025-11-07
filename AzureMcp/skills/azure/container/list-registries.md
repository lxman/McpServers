# List Container Registries

List Azure container registries.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter
- **resourceGroupName** (string, optional): Resource group filter

## Returns
JSON object with success status and array of registries.

## Example Response
```json
{
  "success": true,
  "registries": [
    {
      "name": "myregistry",
      "resourceGroup": "my-rg",
      "location": "eastus",
      "loginServer": "myregistry.azurecr.io"
    }
  ]
}
```
