# Get Kubernetes Cluster

Get details of a specific AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name

## Returns
JSON object with cluster details.

## Example Response
```json
{
  "success": true,
  "cluster": {
    "name": "mycluster",
    "kubernetesVersion": "1.28.0",
    "nodeResourceGroup": "MC_my-rg_mycluster_eastus",
    "fqdn": "mycluster-dns-12345678.hcp.eastus.azmk8s.io"
  }
}
```
