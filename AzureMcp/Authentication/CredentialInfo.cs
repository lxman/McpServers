namespace AzureMcp.Authentication;

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
    /// Whether this credential was successfully validated
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Error message if credential validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
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

        if (!string.IsNullOrEmpty(AccountName))
            lines.Add($"   Account: {AccountName}");

        if (!string.IsNullOrEmpty(TenantName))
            lines.Add($"   Tenant: {TenantName} ({TenantId})");
        else if (!string.IsNullOrEmpty(TenantId))
            lines.Add($"   Tenant ID: {TenantId}");

        lines.Add($"   Subscriptions: {SubscriptionCount} available");

        if (LastModified.HasValue)
            lines.Add($"   Last used: {LastModified.Value:MMM d, yyyy h:mm tt}");

        return string.Join(Environment.NewLine, lines);
    }
}
