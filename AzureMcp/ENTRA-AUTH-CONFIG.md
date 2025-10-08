# Azure AD/Entra Authentication Configuration Guide

This guide explains the `entra-auth.json` configuration file for AzureMcp.

## File Location

Place `entra-auth.json` in the same directory as `AzureMcp.exe`:
```
AzureMcp/bin/Release/net9.0/
├── AzureMcp.exe
├── entra-auth.json          ← Your configuration
├── entra-auth.example.json  ← Example template
└── [other DLLs...]
```

## Configuration Fields

### Basic Configuration

#### `TenantId` (string, optional)
Azure AD Tenant ID.
- **`"common"`** - Multi-tenant apps (works with any Azure AD tenant)
- **`"organizations"`** - Any organizational account
- **`"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"`** - Specific tenant ID
- **`null`** - Uses `"common"` by default

**Example:**
```json
"TenantId": "common"
```

#### `ClientId` (string, optional)
Application (Client) ID from Azure AD app registration.
- **`null`** - Uses Azure CLI's well-known client ID (recommended for interactive scenarios)
- **`"your-app-client-id"`** - Your own app registration's client ID

**Example:**
```json
"ClientId": null
```

---

### Interactive Browser Authentication

#### `EnableInteractiveBrowser` (boolean)
Enable interactive browser authentication.
- **`true`** - Allow browser-based authentication
- **`false`** - Disable browser authentication

**Example:**
```json
"EnableInteractiveBrowser": true
```

#### `RedirectUri` (string, optional)
Redirect URI for browser authentication.
- **Default:** `"http://localhost:8400"`
- Must match redirect URI configured in Azure AD app registration (if using custom app)

**Example:**
```json
"RedirectUri": "http://localhost:8400"
```

#### `BrowserTimeoutSeconds` (integer)
Browser authentication timeout in seconds.
- **Default:** `300` (5 minutes)
- Range: `60` - `600`

**Example:**
```json
"BrowserTimeoutSeconds": 300
```

---

### Device Code Flow

#### `EnableDeviceCode` (boolean)
Enable device code flow authentication (for SSH/remote scenarios).
- **`true`** - Allow device code authentication
- **`false`** - Disable device code flow

**Example:**
```json
"EnableDeviceCode": true
```

#### `DeviceCodeTimeoutSeconds` (integer)
Device code authentication timeout in seconds.
- **Default:** `300` (5 minutes)
- Range: `60` - `600`

**Example:**
```json
"DeviceCodeTimeoutSeconds": 300
```

---

### Service Principal (Non-Interactive)

#### `ClientSecret` (string, optional)
Client secret for service principal authentication.
- **`null`** - No client secret configured
- **`"your-client-secret"`** - Your app registration's client secret

**Required for service principal authentication:**
- `TenantId`
- `ClientId`
- `ClientSecret`

**Example:**
```json
"ClientSecret": "your-secret-here"
```

⚠️ **Security Note:** Keep this file secure! Client secrets provide access to your Azure resources.

---

### Certificate-Based Authentication

#### `CertificatePath` (string, optional)
Path to certificate file (.pfx or .p12).
- **`null`** - No certificate configured
- **`"C:\\path\\to\\cert.pfx"`** - Full path to certificate file

**Example:**
```json
"CertificatePath": "C:\\certs\\azure-cert.pfx"
```

#### `CertificatePassword` (string, optional)
Password for encrypted certificate file.
- **`null`** - Certificate is not encrypted
- **`"password"`** - Certificate password

**Example:**
```json
"CertificatePassword": "cert-password"
```

#### `CertificateThumbprint` (string, optional)
Certificate thumbprint (alternative to file path - uses Windows certificate store).
- **`null`** - Not using certificate store
- **`"ABC123..."`** - Certificate thumbprint

**Example:**
```json
"CertificateThumbprint": "1234567890ABCDEF1234567890ABCDEF12345678"
```

---

### Managed Identity

#### `EnableManagedIdentity` (boolean)
Enable managed identity authentication (only works on Azure infrastructure).
- **`true`** - Use managed identity
- **`false`** - Don't use managed identity

**Only available on:**
- Azure Virtual Machines
- Azure App Service
- Azure Container Apps
- Azure Kubernetes Service
- Azure Functions

**Example:**
```json
"EnableManagedIdentity": false
```

#### `ManagedIdentityClientId` (string, optional)
Client ID for user-assigned managed identity.
- **`null`** - Use system-assigned managed identity
- **`"client-id"`** - Use user-assigned managed identity

**Example:**
```json
"ManagedIdentityClientId": null
```

---

### Token Cache Settings

#### `EnableTokenCache` (boolean)
Enable persistent token caching.
- **`true`** - Cache tokens for reuse (recommended)
- **`false`** - Don't cache tokens (requires re-authentication every time)

**Example:**
```json
"EnableTokenCache": true
```

#### `TokenCacheName` (string, optional)
Custom name for token cache.
- **Default:** `"azure-mcp-cache"`
- Helps isolate tokens from other applications

**Example:**
```json
"TokenCacheName": "azure-mcp-cache"
```

---

### Advanced Settings

#### `AuthorityHost` (string, optional)
Custom authority URL for sovereign clouds.
- **`null`** - Use default: `https://login.microsoftonline.com/`
- **Azure Government:** `https://login.microsoftonline.us/`
- **Azure China:** `https://login.chinacloudapi.cn/`
- **Azure Germany:** `https://login.microsoftonline.de/`

**Example:**
```json
"AuthorityHost": null
```

#### `AdditionalScopes` (array, optional)
Additional scopes to request during authentication.
- **Default:** `[]` (empty array)
- **Common scopes:**
  - `"https://management.azure.com/.default"` - Azure Resource Manager
  - `"https://graph.microsoft.com/.default"` - Microsoft Graph

**Example:**
```json
"AdditionalScopes": [
  "https://management.azure.com/.default"
]
```

---

## Example Configurations

### Example 1: Development (Interactive Browser)
```json
{
  "TenantId": "common",
  "ClientId": null,
  "EnableInteractiveBrowser": true,
  "EnableDeviceCode": false,
  "EnableTokenCache": true
}
```

### Example 2: Remote/SSH (Device Code)
```json
{
  "TenantId": "common",
  "ClientId": null,
  "EnableInteractiveBrowser": false,
  "EnableDeviceCode": true,
  "EnableTokenCache": true
}
```

### Example 3: Service Principal (Automation)
```json
{
  "TenantId": "12345678-1234-1234-1234-123456789012",
  "ClientId": "87654321-4321-4321-4321-210987654321",
  "ClientSecret": "your-secret-here",
  "EnableTokenCache": false
}
```

### Example 4: Certificate-Based (Production)
```json
{
  "TenantId": "12345678-1234-1234-1234-123456789012",
  "ClientId": "87654321-4321-4321-4321-210987654321",
  "CertificatePath": "C:\\certs\\azure-prod.pfx",
  "CertificatePassword": "cert-password",
  "EnableTokenCache": true
}
```

### Example 5: Managed Identity (Azure VM)
```json
{
  "EnableManagedIdentity": true,
  "ManagedIdentityClientId": null,
  "EnableTokenCache": false
}
```

---

## Troubleshooting

### Configuration not being used
- Verify file is named exactly `entra-auth.json`
- Ensure file is in the same directory as `AzureMcp.exe`
- Check JSON syntax using a JSON validator
- Check logs in `azure-discovery.log`

### Interactive browser not opening
- Ensure `EnableInteractiveBrowser` is `true`
- Check firewall settings for localhost
- Verify `RedirectUri` matches app registration (if using custom app)

### Certificate authentication failing
- Verify certificate file path is correct
- Check certificate is not expired
- Ensure certificate password is correct (if encrypted)
- Verify certificate thumbprint format (no spaces or dashes)

### Managed identity not working
- Ensure running on Azure infrastructure with managed identity enabled
- Verify managed identity has necessary permissions
- Check if using user-assigned MI with correct client ID

---

## Security Best Practices

1. **Never commit secrets to source control**
   - Add `entra-auth.json` to `.gitignore`
   - Use environment variables or Azure Key Vault for production

2. **Use certificate-based auth for production**
   - More secure than client secrets
   - Harder to accidentally expose

3. **Use managed identity when possible**
   - No credentials to manage
   - Most secure option for Azure-hosted apps

4. **Rotate secrets regularly**
   - Client secrets should be rotated every 90-180 days
   - Certificates should be renewed before expiration

5. **Use least-privilege permissions**
   - Only grant necessary Azure permissions
   - Use separate service principals for different environments

---

## Need Help?

See the main README.md for:
- Available authentication tools
- Usage examples
- Common scenarios
- Troubleshooting guide
