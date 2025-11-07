# Regenerate Registry Credential

Regenerate admin password for a container registry.

## Parameters
- **subscriptionId** (string): Azure subscription ID
- **resourceGroupName** (string): Resource group name
- **registryName** (string): Registry name
- **passwordName** (string): Password name to regenerate (password or password2)

## Returns
JSON object with regenerated credentials.

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
