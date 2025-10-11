namespace AzureServer.Services.KeyVault.Models;

public class SecretPropertiesDto
{
    public string Name { get; set; } = string.Empty;
    public string VaultName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? CreatedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public DateTime? NotBefore { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public bool Managed { get; set; }
    public string? RecoveryLevel { get; set; }
    public int? RecoverableDays { get; set; }
}
