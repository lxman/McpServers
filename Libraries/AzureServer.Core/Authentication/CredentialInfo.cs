namespace AzureServer.Core.Authentication;

/// <summary>
/// Information about a discovered Azure credential
/// </summary>
public class CredentialInfo
{
    /// <summary>
    /// Unique identifier for this credential (for selection)
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Source of the credential (CLI, VisualStudio, Environment, etc.)
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Authentication method used (auto-discovered, interactive-browser, device-code, client-secret, certificate, managed-identity)
    /// </summary>
    public string? AuthenticationMethod { get; set; }
    
    /// <summary>
    /// User account name/email if available
    /// </summary>
    public string? AccountName { get; set; }
    
    /// <summary>
    /// Tenant ID
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Tenant display name if available
    /// </summary>
    public string? TenantName { get; set; }
    
    /// <summary>
    /// List of subscription IDs accessible with this credential
    /// </summary>
    public List<string> SubscriptionIds { get; set; } = [];
    
    /// <summary>
    /// Number of subscriptions accessible
    /// </summary>
    public int SubscriptionCount => SubscriptionIds.Count;
    
    /// <summary>
    /// Last modified timestamp if available
    /// </summary>
    public DateTime? LastModified { get; set; }
    
    /// <summary>
    /// Token expiration time (if applicable)
    /// </summary>
    public DateTimeOffset? TokenExpiresOn { get; set; }
    
    /// <summary>
    /// Whether this credential supports token refresh
    /// </summary>
    public bool SupportsRefresh { get; set; }
    
    /// <summary>
    /// Whether this credential was successfully validated
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Error message if credential validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Client ID used for authentication (if applicable)
    /// </summary>
    public string? ClientId { get; set; }
    
    /// <summary>
    /// Whether this is an interactive credential (required user interaction)
    /// </summary>
    public bool IsInteractive { get; set; }
    
    /// <summary>
    /// Additional metadata about the credential
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Format credential info for display to user
    /// </summary>
    public string FormatForDisplay(int index)
    {
        var lines = new List<string>
        {
            $"{index}. {Source}"
        };

        if (!string.IsNullOrEmpty(AuthenticationMethod))
            lines.Add($"   Method: {AuthenticationMethod}");

        if (!string.IsNullOrEmpty(AccountName))
            lines.Add($"   Account: {AccountName}");

        if (!string.IsNullOrEmpty(TenantName))
            lines.Add($"   Tenant: {TenantName} ({TenantId})");
        else if (!string.IsNullOrEmpty(TenantId))
            lines.Add($"   Tenant ID: {TenantId}");

        lines.Add($"   Subscriptions: {SubscriptionCount} available");

        if (TokenExpiresOn.HasValue)
        {
            TimeSpan timeToExpiry = TokenExpiresOn.Value - DateTimeOffset.UtcNow;
            if (timeToExpiry.TotalMinutes > 0)
                lines.Add($"   Token expires: {timeToExpiry.TotalHours:F1} hours");
            else
                lines.Add($"   Token expired: {Math.Abs(timeToExpiry.TotalHours):F1} hours ago");
        }

        if (LastModified.HasValue)
            lines.Add($"   Last used: {LastModified.Value:MMM d, yyyy h:mm tt}");

        return string.Join(Environment.NewLine, lines);
    }
}
