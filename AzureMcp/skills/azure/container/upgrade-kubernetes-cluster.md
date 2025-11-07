# Upgrade Kubernetes Cluster

Upgrade an AKS cluster to a new Kubernetes version.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name
- **kubernetesVersion** (string): Target Kubernetes version

## Returns
JSON object with success status and upgraded cluster.

## Example Response
```json
{
  "success": true,
  "cluster": {
    "name": "mycluster",
    "kubernetesVersion": "1.28.0",
    "provisioningState": "Upgrading"
  }
}
```
