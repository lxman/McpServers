# List Application Insights

List Application Insights components.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter
- **resourceGroupName** (string, optional): Resource group filter

## Returns
JSON object with array of Application Insights components.

## Example Response
```json
{
  "success": true,
  "components": [
    {
      "name": "myappinsights",
      "resourceGroup": "my-rg",
      "location": "eastus",
      "instrumentationKey": "abc-123-def-456",
      "applicationType": "web"
    }
  ]
}
```
