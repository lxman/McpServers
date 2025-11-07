# Delete Image

Delete a container image.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **repositoryName** (string): Repository name
- **tag** (string): Image tag

## Returns
JSON object with success status and deletion result.

## Example Response
```json
{
  "success": true,
  "message": "Image deleted"
}
```
