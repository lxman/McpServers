# List Images

List container images in a registry or repository.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **repositoryName** (string, optional): Repository name filter

## Returns
JSON object with success status and array of images.

## Example Response
```json
{
  "success": true,
  "images": [
    {
      "repository": "myapp",
      "tag": "latest",
      "digest": "sha256:..."
    }
  ]
}
```
