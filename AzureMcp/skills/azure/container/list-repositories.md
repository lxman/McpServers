# List Repositories

List repositories in a container registry.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name

## Returns
JSON object with success status and array of repositories.

## Example Response
```json
{
  "success": true,
  "repositories": ["myapp", "myservice"]
}
```
