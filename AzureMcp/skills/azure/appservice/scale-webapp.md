# Scale Web App

Scale a web app to specified instance count.

## Parameters
- **webAppName** (string): Web app name
- **resourceGroupName** (string): Resource group name
- **instanceCount** (int): Number of instances (1-30)
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with success status and scaling result.

## Example Response
```json
{
  "success": true,
  "message": "Scaled to 3 instances"
}
```
