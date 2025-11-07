# List Kubernetes Clusters

List Azure Kubernetes Service (AKS) clusters.

## Parameters
- **subscriptionId** (string, optional): Azure subscription ID filter
- **resourceGroupName** (string, optional): Resource group filter

## Returns
JSON object with success status and array of clusters.

## Example Response
```json
{
  "success": true,
  "clusters": [
    {
      "name": "mycluster",
      "resourceGroup": "my-rg",
      "location": "eastus",
      "kubernetesVersion": "1.28.0"
    }
  ]
}
```
