# Get Application Insights

Get details of an Application Insights component.

## Parameters
- **resourceGroupName** (string): Resource group name
- **componentName** (string): Component name
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with component details.

## Example Response
```json
{
  "success": true,
  "component": {
    "name": "myappinsights",
    "resourceGroup": "my-rg",
    "location": "eastus",
    "instrumentationKey": "abc-123-def-456",
    "applicationType": "web",
    "connectionString": "InstrumentationKey=abc-123-def-456"
  }
}
```
