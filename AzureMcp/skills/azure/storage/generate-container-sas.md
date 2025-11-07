# Generate Container SAS

Generate a Shared Access Signature (SAS) URL for a container.

## Parameters
- **accountName** (string): Storage account name
- **containerName** (string): Container name
- **expiryHours** (int, optional): Expiry time in hours (default: 1)
- **permissions** (string, optional): Permissions string (default: "rl")

## Returns
JSON object with SAS URL.

## Example Response
```json
{
  "success": true,
  "sasUrl": "https://account.blob.core.windows.net/container?sv=2021-06-08&se=...",
  "expiresIn": "1 hours",
  "permissions": "rl"
}
```
