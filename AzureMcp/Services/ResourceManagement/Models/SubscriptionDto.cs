namespace AzureMcp.Services.ResourceManagement.Models;

public class SubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
