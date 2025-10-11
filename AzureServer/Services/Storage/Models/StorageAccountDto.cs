namespace AzureServer.Services.Storage.Models;

public class StorageAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? ProvisioningState { get; set; }
    public string? PrimaryLocation { get; set; }
    public string? SecondaryLocation { get; set; }
    public DateTime? CreationTime { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public bool? EnableHttpsTrafficOnly { get; set; }
    public string? MinimumTlsVersion { get; set; }
    public bool? AllowBlobPublicAccess { get; set; }
}