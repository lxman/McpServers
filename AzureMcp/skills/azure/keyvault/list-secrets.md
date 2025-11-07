# List Secrets

List secrets in an Azure Key Vault.

## Parameters
- **vaultName** (string): Key Vault name

## Returns
JSON object with success status and array of secrets.

## Example Response
```json
{
  "success": true,
  "secrets": [
    {
      "name": "mysecret",
      "enabled": true,
      "createdOn": "2025-01-01T00:00:00Z",
      "updatedOn": "2025-01-10T12:00:00Z",
      "expiresOn": null,
      "contentType": "text/plain"
    }
  ]
}
```
