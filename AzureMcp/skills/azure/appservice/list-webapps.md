# List Web Apps

List Azure App Service web apps.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter
- **resourceGroupName** (string, optional): Resource group filter

## Returns
JSON object with success status and array of web apps.

## Example Response
```json
{
  "success": true,
  "webApps": [
    {
      "name": "mywebapp",
      "resourceGroup": "my-rg",
      "location": "eastus",
      "state": "Running",
      "defaultHostName": "mywebapp.azurewebsites.net"
    }
  ]
}
```
