# Delete Repository

Delete a repository and all its images.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **repositoryName** (string): Repository name

## Returns
JSON object with success status and deletion result.

## Example Response
```json
{
  "success": true,
  "message": "Repository deleted"
}
```
