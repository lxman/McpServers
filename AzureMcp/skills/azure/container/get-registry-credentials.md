# Get Registry Credentials

Get admin credentials for a container registry.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name

## Returns
JSON object with registry credentials.

## Example Response
```json
{
  "success": true,
  "credentials": {
    "username": "myregistry",
    "password": "**********",
    "password2": "**********"
  }
}
```
