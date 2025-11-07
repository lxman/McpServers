# Start Kubernetes Cluster

Start a stopped AKS cluster.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **clusterName** (string): Cluster name

## Returns
JSON object with success status and started cluster.

## Example Response
```json
{
  "success": true,
  "cluster": {
    "name": "mycluster",
    "powerState": "Running"
  }
}
```
