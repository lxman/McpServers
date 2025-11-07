# Get Account Info

Get Azure account information including credentials, subscriptions, and tenant details.

## Parameters
None

## Returns
JSON object with account information including credential source, subscriptions, and tenant details.

## Example Response
```json
{
  "success": true,
  "accountInfo": {
    "credentialSource": "AzureCli",
    "credentialId": "user@example.com",
    "accountName": "user@example.com",
    "tenantId": "00000000-0000-0000-0000-000000000000",
    "tenantName": "Example Tenant",
    "subscriptionCount": 2,
    "subscriptions": [
      {
        "subscriptionId": "sub-id",
        "displayName": "Production",
        "state": "Enabled",
        "tenantId": "tenant-id"
      }
    ]
  }
}
```
