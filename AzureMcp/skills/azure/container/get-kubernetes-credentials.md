# Get Kubernetes Credentials

Get kubeconfig credentials for an AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name

## Returns
JSON object with kubeconfig credentials.

## Example Response
```json
{
  "success": true,
  "credentials": {
    "kubeconfig": "apiVersion: v1\nclusters:\n- cluster:..."
  }
}
```
