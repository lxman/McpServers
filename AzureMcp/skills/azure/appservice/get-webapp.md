# Get Web App

Get details of a specific web app.

## Parameters
- **webAppName** (string): Web app name
- **resourceGroupName** (string): Resource group name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with web app details.

## Example Response
```json
{
  "success": true,
  "webApp": {
    "name": "mywebapp",
    "resourceGroup": "my-rg",
    "location": "eastus",
    "state": "Running",
    "defaultHostName": "mywebapp.azurewebsites.net",
    "appServicePlan": "myplan"
  }
}
```
