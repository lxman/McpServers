# List Deployment Slots

List deployment slots for a web app.

## Parameters
- **webAppName** (string): Web app name
- **resourceGroupName** (string): Resource group name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with success status and array of deployment slots.

## Example Response
```json
{
  "success": true,
  "slots": [
    {
      "name": "staging",
      "state": "Running",
      "hostName": "mywebapp-staging.azurewebsites.net"
    }
  ]
}
```
