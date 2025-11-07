# List App Service Plans

List Azure App Service plans.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter
- **resourceGroupName** (string, optional): Resource group filter

## Returns
JSON object with success status and array of app service plans.

## Example Response
```json
{
  "success": true,
  "plans": [
    {
      "name": "myplan",
      "resourceGroup": "my-rg",
      "sku": "S1",
      "tier": "Standard",
      "capacity": 1
    }
  ]
}
```
