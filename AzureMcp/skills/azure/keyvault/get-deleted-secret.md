# Get Deleted Secret

Get details of a deleted secret.

## Parameters
- **vaultName** (string): Key Vault name
- **secretName** (string): Deleted secret name

## Returns
JSON object with deleted secret details.

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
