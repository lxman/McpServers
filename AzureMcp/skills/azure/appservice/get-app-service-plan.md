# Get App Service Plan

Get details of a specific app service plan.

## Parameters
- **planName** (string): App service plan name
- **resourceGroupName** (string): Resource group name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with app service plan details.

## Example Response
```json
{
  "success": true,
  "plan": {
    "name": "myplan",
    "resourceGroup": "my-rg",
    "sku": "S1",
    "tier": "Standard",
    "capacity": 1,
    "status": "Ready"
  }
}
```
