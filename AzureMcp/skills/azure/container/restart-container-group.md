# Restart Container Group

Restart an Azure container group.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **containerGroupName** (string): Container group name

## Returns
JSON object with success status and updated container group.

## Example Response
```json
{
  "success": true,
  "containerGroup": {
    "name": "mycontainergroup",
    "state": "Running"
  }
}
```
