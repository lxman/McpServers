# Create Container Registry

Create a new Azure container registry.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **request** (ContainerRegistryCreateRequest): Registry creation request

## Returns
JSON object with created registry details.

## Example Response
```json
{
  "success": true,
  "registry": {
    "name": "myregistry",
    "loginServer": "myregistry.azurecr.io",
    "sku": "Basic"
  }
}
```
