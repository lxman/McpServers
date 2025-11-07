# Update Secret Properties

Update properties of a secret without changing its value.

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Secret name
- **version** (string, optional): Secret version
- **enabled** (bool, optional): Enable/disable secret
- **expiresOn** (string, optional): Expiration date (ISO 8601)
- **notBefore** (string, optional): Not before date (ISO 8601)
- **contentType** (string, optional): Content type
- **tags** (Dictionary<string, string>, optional): Tags

## Returns
JSON object with updated secret properties.

## Example Response
```json
{
  "success": true,
  "properties": {
    "name": "mysecret",
    "enabled": true,
    "updatedOn": "2025-01-10T12:00:00Z",
    "expiresOn": "2026-01-10T00:00:00Z"
  }
}
```
