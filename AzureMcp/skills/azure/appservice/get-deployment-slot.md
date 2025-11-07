# Get Deployment Slot

Get details of a specific deployment slot.

## Parameters
- **webAppName** (string): Web app name
- **slotName** (string): Deployment slot name
- **resourceGroupName** (string): Resource group name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with deployment slot details.

## Example Response
```json
{
  "success": true,
  "slot": {
    "name": "staging",
    "state": "Running",
    "hostName": "mywebapp-staging.azurewebsites.net"
  }
}
```
