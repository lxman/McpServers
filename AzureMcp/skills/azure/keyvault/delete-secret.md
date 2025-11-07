# Delete Secret

Delete a secret from Key Vault (soft delete).

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Secret name to delete

## Returns
JSON object with deleted secret information.

## Example Response
```json
{
  "success": true,
  "deletedSecret": {
    "name": "mysecret",
    "deletedOn": "2025-01-10T12:00:00Z",
    "scheduledPurgeDate": "2025-04-10T12:00:00Z",
    "recoveryId": "https://vault.vault.azure.net/deletedsecrets/mysecret"
  }
}
```
