# List Container Groups

List Azure container groups.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter
- **resourceGroupName** (string, optional): Resource group filter

## Returns
JSON object with success status and array of container groups.

## Example Response
```json
{
  "success": true,
  "containerGroups": [
    {
      "name": "mycontainergroup",
      "resourceGroup": "my-rg",
      "location": "eastus",
      "state": "Running"
    }
  ]
}
```
