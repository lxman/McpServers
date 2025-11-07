# Recover Deleted Secret

Recover a soft-deleted secret in Key Vault.

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Secret name to recover

## Returns
JSON object with recovered secret properties.

## Example Response
```json
{
  "success": true,
  "properties": {
    "name": "mysecret",
    "enabled": true,
    "createdOn": "2025-01-01T00:00:00Z",
    "updatedOn": "2025-01-10T12:05:00Z"
  }
}
```
