# Run Build Task

Run a container registry build task.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **buildTaskName** (string): Build task name

## Returns
JSON object with build run details.

## Example Response
```json
{
  "success": true,
  "buildRun": {
    "runId": "ca1",
    "status": "Started"
  }
}
```
