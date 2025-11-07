# Create Kubernetes Cluster

Create a new AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **request** (KubernetesClusterCreateRequest): Cluster creation request

## Returns
JSON object with created cluster details.

## Example Response
```json
{
  "success": true,
  "cluster": {
    "name": "mycluster",
    "kubernetesVersion": "1.28.0",
    "provisioningState": "Creating"
  }
}
```
