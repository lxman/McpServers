# Create Container Group

Create a new Azure container group.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **request** (ContainerGroupCreateRequest): Container group creation request object

## Returns
JSON object with created container group details.

## Example Response
```json
{
  "success": true,
  "containerGroup": {
    "name": "mycontainergroup",
    "resourceGroup": "my-rg",
    "location": "eastus",
    "state": "Pending"
  }
}
```
