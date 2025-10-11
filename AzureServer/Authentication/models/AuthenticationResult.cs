namespace AzureServer.Authentication.models;

/// <summary>
/// Result from an authentication operation
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Whether authentication was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Authentication method used
    /// </summary>
    public string? AuthMethod { get; set; }
    
    /// <summary>
    /// Human-readable message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Error code if authentication failed
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// When the access token expires (if applicable)
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }
    
    /// <summary>
    /// Account/user that was authenticated
    /// </summary>
    public string? AccountName { get; set; }
    
    /// <summary>
    /// Tenant ID authenticated against
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Credential ID for future reference
    /// </summary>
    public string? CredentialId { get; set; }
    
    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Result from device code authentication initiation
/// </summary>
public class DeviceCodeResult
{
    /// <summary>
    /// Whether device code flow was initiated successfully
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// User-friendly code to enter (e.g., "A1B2C3D4")
    /// </summary>
    public string? UserCode { get; set; }
    
    /// <summary>
    /// URL where user should enter the code
    /// </summary>
    public string? VerificationUri { get; set; }
    
    /// <summary>
    /// When the device code expires
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; set; }
    
    /// <summary>
    /// Polling interval in seconds
    /// </summary>
    public int? PollIntervalSeconds { get; set; }
    
    /// <summary>
    /// Human-readable message with instructions
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message if initiation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// The credential ID for future reference
    /// </summary>
    public string? CredentialId { get; set; }
}
