# Set Secret

Set or update a secret value in Key Vault.

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Secret name
- **value** (string): Secret value
- **contentType** (string, optional): Content type
- **expiresOn** (string, optional): Expiration date (ISO 8601)
- **notBefore** (string, optional): Not before date (ISO 8601)
- **tags** (Dictionary<string, string>, optional): Tags

## Returns
JSON object with set secret properties.

## Example Response
```json
{
  "success": true,
  "secret": {
    "name": "mysecret",
    "value": "secret-value",
    "enabled": true,
    "createdOn": "2025-01-10T12:00:00Z"
  }
}
```
