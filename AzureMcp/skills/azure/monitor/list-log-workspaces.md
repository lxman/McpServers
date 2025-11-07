# List Log Workspaces

List Azure Monitor Log Analytics workspaces.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter

## Returns
JSON object with array of workspace IDs.

## Example Response
```json
{
  "success": true,
  "workspaces": [
    "workspace-id-1",
    "workspace-id-2"
  ]
}
```
