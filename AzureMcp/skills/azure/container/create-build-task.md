# Create Build Task

Create a container registry build task.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **request** (BuildTaskCreateRequest): Build task creation request

## Returns
JSON object with created build task details.

## Example Response
```json
{
  "success": true,
  "buildTask": {
    "name": "mybuildtask",
    "status": "Enabled"
  }
}
```
