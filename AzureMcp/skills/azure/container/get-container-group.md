# Get Container Group

Get details of a specific container group.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **containerGroupName** (string): Container group name

## Returns
JSON object with container group details.

## Example Response
```json
{
  "success": true,
  "containerGroup": {
    "name": "mycontainergroup",
    "resourceGroup": "my-rg",
    "location": "eastus",
    "state": "Running",
    "containers": []
  }
}
```
