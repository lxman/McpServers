# Execute Container Command

Execute a command in a running container.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **containerGroupName** (string): Container group name
- **containerName** (string): Container name
- **command** (string): Command to execute

## Returns
JSON object with command execution result.

## Example Response
```json
{
  "success": true,
  "result": {
    "output": "Command output...",
    "exitCode": 0
  }
}
```
