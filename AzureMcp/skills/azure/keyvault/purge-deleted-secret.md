# Purge Deleted Secret

Permanently purge a soft-deleted secret from Key Vault.

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Secret name to purge

## Returns
JSON object with success status.

## Example Response
```json
{
  "success": true,
  "message": "Secret mysecret permanently purged from vault myvault"
}
```
