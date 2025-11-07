# Create Alert

Create a new alert rule in Azure Monitor.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **alertName** (string): Alert name
- **description** (string): Alert description
- **severity** (string): Severity level (0-4)
- **workspaceId** (string): Log Analytics workspace ID
- **query** (string): KQL query for alert
- **evaluationFrequency** (string): Evaluation frequency (e.g., "PT5M")
- **windowSize** (string): Time window (e.g., "PT15M")

## Returns
JSON object with created alert details.

## Example Response
```json
{
  "success": true,
  "alert": {
    "name": "HighCPUAlert",
    "severity": "2",
    "enabled": true,
    "evaluationFrequency": "PT5M",
    "windowSize": "PT15M"
  }
}
```
