# List Alerts

List alert rules in Azure Monitor.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter
- **resourceGroupName** (string, optional): Resource group filter

## Returns
JSON object with array of alert rules.

## Example Response
```json
{
  "success": true,
  "alerts": [
    {
      "name": "HighCPUAlert",
      "resourceGroup": "my-rg",
      "severity": "2",
      "enabled": true,
      "description": "Alert when CPU is high"
    }
  ]
}
```
