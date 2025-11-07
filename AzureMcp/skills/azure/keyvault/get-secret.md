# Get Secret

Get secret value from Key Vault.

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Secret name
- **version** (string, optional): Secret version

## Returns
JSON object with secret value and properties.

## Example Response
```json
{
  "success": true,
  "secret": {
    "name": "mysecret",
    "value": "secret-value",
    "enabled": true,
    "createdOn": "2025-01-01T00:00:00Z",
    "updatedOn": "2025-01-10T12:00:00Z"
  }
}
```
