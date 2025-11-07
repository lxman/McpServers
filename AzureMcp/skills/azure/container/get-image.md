# Get Image

Get details of a specific container image.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **repositoryName** (string): Repository name
- **tag** (string): Image tag

## Returns
JSON object with image details.

## Example Response
```json
{
  "success": true,
  "image": {
    "repository": "myapp",
    "tag": "latest",
    "digest": "sha256:...",
    "createdTime": "2025-01-10T12:00:00Z"
  }
}
```
