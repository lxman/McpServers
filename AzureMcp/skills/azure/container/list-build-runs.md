# List Build Runs

List container registry build runs.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name

## Returns
JSON object with success status and array of build runs.

## Example Response
```json
{
  "success": true,
  "buildRuns": [
    {
      "runId": "ca1",
      "status": "Succeeded",
      "startTime": "2025-01-10T12:00:00Z"
    }
  ]
}
```
