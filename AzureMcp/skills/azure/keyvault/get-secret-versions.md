# Get Secret Versions

Get all versions of a secret.

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Secret name

## Returns
JSON object with array of secret versions.

## Example Response
```json
{
  "success": true,
  "versions": [
    {
      "name": "mysecret",
      "version": "abc123",
      "enabled": true,
      "createdOn": "2025-01-01T00:00:00Z",
      "updatedOn": "2025-01-01T00:00:00Z"
    }
  ]
}
```
