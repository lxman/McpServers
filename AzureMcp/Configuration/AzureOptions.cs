namespace AzureMcp.Configuration;

/// <summary>
/// Configuration options for Azure services
/// </summary>
public class AzureOptions
{
    /// <summary>
    /// Azure tenant ID (optional)
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Azure subscription ID (optional)
    /// </summary>
    public string? SubscriptionId { get; set; }
    
    /// <summary>
    /// Azure cloud authority host (optional, defaults to public cloud)
    /// </summary>
    public string? AuthorityHost { get; set; }
    
    /// <summary>
    /// Resource group name for operations (optional)
    /// </summary>
    public string? ResourceGroupName { get; set; }
    
    /// <summary>
    /// Default Azure region for resource operations
    /// </summary>
    public string DefaultRegion { get; set; } = "East US";
    
    /// <summary>
    /// Whether to enable Azure AD authentication for additional services
    /// </summary>
    public bool EnableAzureAdAuth { get; set; } = false;
}
