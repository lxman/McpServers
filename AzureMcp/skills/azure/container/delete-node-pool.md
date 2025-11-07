# Delete Node Pool

Delete a node pool from an AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name
- **nodePoolName** (string): Node pool name to delete

## Returns
JSON object with success status and deletion result.

## Example Response
```json
{
  "success": true,
  "message": "Node pool deleted"
}
```
