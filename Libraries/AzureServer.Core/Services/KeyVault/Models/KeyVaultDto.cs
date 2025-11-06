namespace AzureServer.Core.Services.KeyVault.Models;

public class KeyVaultDto
{
    public string Name { get; set; } = string.Empty;
    public string VaultUri { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ResourceId { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
