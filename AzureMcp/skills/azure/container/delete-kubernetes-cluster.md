# Delete Kubernetes Cluster

Delete an AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name to delete

## Returns
JSON object with success status and deletion result.

## Example Response
```json
{
  "success": true,
  "message": "Cluster deleted"
}
```
