# Get Node Pool

Get details of a specific node pool.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name
- **nodePoolName** (string): Node pool name

## Returns
JSON object with node pool details.

## Example Response
```json
{
  "success": true,
  "nodePool": {
    "name": "nodepool1",
    "count": 3,
    "vmSize": "Standard_DS2_v2",
    "mode": "System",
    "provisioningState": "Succeeded"
  }
}
```
