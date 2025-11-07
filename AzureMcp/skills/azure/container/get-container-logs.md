# Get Container Logs

Get logs from a container in a container group.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **containerGroupName** (string): Container group name
- **containerName** (string): Container name
- **tail** (int, optional): Number of lines to tail

## Returns
JSON object with container logs.

## Example Response
```json
{
  "success": true,
  "logs": "Container log output..."
}
```
