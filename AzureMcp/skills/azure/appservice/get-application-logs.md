# Get Application Logs

Get application logs for a web app.

## Parameters
- **webAppName** (string): Web app name
- **resourceGroupName** (string): Resource group name
- **lastHours** (int, optional): Number of hours to retrieve logs for
- **subscriptionId** (string, optional): Azure subscription ID

## Returns
JSON object with application logs.

## Example Response
```json
{
  "success": true,
  "logs": [
    {
      "timestamp": "2025-01-10T12:00:00Z",
      "level": "Information",
      "message": "Application started"
    }
  ],
  "note": "Full log streaming requires Kudu API or Azure Monitor integration"
}
```
