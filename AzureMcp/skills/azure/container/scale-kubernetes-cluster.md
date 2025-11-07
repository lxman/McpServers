# Scale Kubernetes Cluster

Scale an AKS cluster node pool.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name
- **nodePoolName** (string): Node pool name
- **nodeCount** (int): Target node count

## Returns
JSON object with success status and scaled cluster.

## Example Response
```json
{
  "success": true,
  "cluster": {
    "name": "mycluster",
    "agentPoolProfiles": [
      {
        "name": "nodepool1",
        "count": 3
      }
    ]
  }
}
```
