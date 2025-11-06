namespace AzureServer.Core.Authentication.models;

/// <summary>
/// Unified configuration for all Azure AD/Entra authentication methods
/// </summary>
public class EntraAuthConfig
{
    /// <summary>
    /// Azure AD Tenant ID (or "common" for multi-tenant, "organizations" for any org)
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Azure AD Application (Client) ID
    /// If not specified, uses Azure CLI's well-known client ID for interactive/device flows
    /// </summary>
    public string? ClientId { get; set; }
    
    // === Service Principal (Client Credentials) ===
    
    /// <summary>
    /// Client Secret for service principal authentication
    /// </summary>
    public string? ClientSecret { get; set; }
    
    // === Certificate-based Authentication ===
    
    /// <summary>
    /// Path to certificate file (.pfx/.p12) for certificate-based authentication
    /// </summary>
    public string? CertificatePath { get; set; }
    
    /// <summary>
    /// Password for certificate file (if encrypted)
    /// </summary>
    public string? CertificatePassword { get; set; }
    
    /// <summary>
    /// Certificate thumbprint (alternative to file path - uses certificate store)
    /// </summary>
    public string? CertificateThumbprint { get; set; }
    
    // === Interactive Browser Authentication ===
    
    /// <summary>
    /// Enable interactive browser authentication option
    /// </summary>
    public bool EnableInteractiveBrowser { get; set; } = false;
    
    /// <summary>
    /// Redirect URI for interactive browser flow (default: http://localhost:8400)
    /// </summary>
    public string? RedirectUri { get; set; }
    
    /// <summary>
    /// Browser timeout in seconds (default: 300 = 5 minutes)
    /// </summary>
    public int BrowserTimeoutSeconds { get; set; } = 300;
    
    // === Device Code Flow ===
    
    /// <summary>
    /// Enable device code flow authentication option
    /// </summary>
    public bool EnableDeviceCode { get; set; } = false;
    
    /// <summary>
    /// Device code timeout in seconds (default: 300 = 5 minutes)
    /// </summary>
    public int DeviceCodeTimeoutSeconds { get; set; } = 300;
    
    // === Managed Identity ===
    
    /// <summary>
    /// Enable managed identity authentication (for Azure VMs, App Service, etc.)
    /// </summary>
    public bool EnableManagedIdentity { get; set; } = false;
    
    /// <summary>
    /// Client ID for user-assigned managed identity (null for system-assigned)
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
    
    // === Token Cache Settings ===
    
    /// <summary>
    /// Custom name for token cache (helps isolate from other applications)
    /// </summary>
    public string? TokenCacheName { get; set; }
    
    /// <summary>
    /// Enable persistent token caching (default: true)
    /// </summary>
    public bool EnableTokenCache { get; set; } = true;
    
    // === Authority and Scope Settings ===
    
    /// <summary>
    /// Custom authority URL (default: https://login.microsoftonline.com/)
    /// Useful for sovereign clouds (e.g., Azure Government, Azure China)
    /// </summary>
    public string? AuthorityHost { get; set; }
    
    /// <summary>
    /// Additional scopes to request during authentication
    /// </summary>
    public List<string> AdditionalScopes { get; set; } = new();
    
    // === Helper Methods ===
    
    /// <summary>
    /// Well-known Azure CLI client ID - works for all Azure AD tenants
    /// </summary>
    public static string AzureCliClientId => "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
    
    /// <summary>
    /// Get effective client ID (user-specified or Azure CLI default)
    /// </summary>
    public string GetEffectiveClientId() => ClientId ?? AzureCliClientId;
    
    /// <summary>
    /// Get effective tenant ID (user-specified or "common" for multi-tenant)
    /// </summary>
    public string GetEffectiveTenantId() => TenantId ?? "common";
    
    /// <summary>
    /// Get effective redirect URI for interactive browser flow
    /// </summary>
    public Uri GetEffectiveRedirectUri() => 
        string.IsNullOrEmpty(RedirectUri) 
            ? new Uri("http://localhost:8400") 
            : new Uri(RedirectUri);
    
    /// <summary>
    /// Check if service principal (client secret) is configured
    /// </summary>
    public bool HasClientSecret => 
        !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
    
    /// <summary>
    /// Check if certificate authentication is configured
    /// </summary>
    public bool HasCertificate => 
        !string.IsNullOrEmpty(CertificatePath) || !string.IsNullOrEmpty(CertificateThumbprint);
}
