# Get Build Log

Get build log for a container registry build run.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **runId** (string): Build run ID

## Returns
JSON object with build log content.

## Example Response
```json
{
  "success": true,
  "log": "Build log output..."
}
```
