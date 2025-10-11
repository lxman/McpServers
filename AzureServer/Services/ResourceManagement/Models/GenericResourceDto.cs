namespace AzureServer.Services.ResourceManagement.Models;

public class GenericResourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? ResourceGroup { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string? Kind { get; set; }
    public string? Sku { get; set; }
    public string? ProvisioningState { get; set; }
    public string? CreatedTime { get; set; }
    public string? ChangedTime { get; set; }
}
