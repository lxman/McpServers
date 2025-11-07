# Swap Deployment Slots

Swap two deployment slots.

## Parameters
- **webAppName** (string): Web app name
- **resourceGroupName** (string): Resource group name
- **sourceSlotName** (string): Source slot name
- **targetSlotName** (string): Target slot name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with success status and swap result.

## Example Response
```json
{
  "success": true,
  "message": "Swapped staging -> production"
}
```
