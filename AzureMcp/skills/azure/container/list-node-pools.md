# List Node Pools

List node pools in an AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name

## Returns
JSON object with success status and array of node pools.

## Example Response
```json
{
  "success": true,
  "nodePools": [
    {
      "name": "nodepool1",
      "count": 3,
      "vmSize": "Standard_DS2_v2",
      "mode": "System"
    }
  ]
}
```
