namespace AzureMcp.Services.ResourceManagement.Models;

public class ResourceGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
    public string? ManagedBy { get; set; }
    public string? ProvisioningState { get; set; }
}
