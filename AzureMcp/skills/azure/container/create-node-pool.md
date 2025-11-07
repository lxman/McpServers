# Create Node Pool

Create a new node pool in an AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name
- **request** (NodePoolCreateRequest): Node pool creation request

## Returns
JSON object with created node pool details.

## Example Response
```json
{
  "success": true,
  "nodePool": {
    "name": "nodepool2",
    "count": 2,
    "vmSize": "Standard_DS3_v2",
    "provisioningState": "Creating"
  }
}
```
