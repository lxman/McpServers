# List Deleted Secrets

List deleted secrets in Key Vault (soft-deleted).

## Parameters
- **vaultName** (string): Key Vault name

## Returns
JSON object with array of deleted secrets.

## Example Response
```json
{
  "success": true,
  "deletedSecrets": [
    {
      "name": "mysecret",
      "deletedOn": "2025-01-10T12:00:00Z",
      "scheduledPurgeDate": "2025-04-10T12:00:00Z",
      "recoveryId": "https://vault.vault.azure.net/deletedsecrets/mysecret"
    }
  ]
}
```
