# Update Node Pool

Update properties of a node pool.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name
- **nodePoolName** (string): Node pool name
- **request** (NodePoolUpdateRequest): Node pool update request

## Returns
JSON object with updated node pool details.

## Example Response
```json
{
  "success": true,
  "nodePool": {
    "name": "nodepool1",
    "count": 5,
    "provisioningState": "Updating"
  }
}
```
