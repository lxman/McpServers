namespace AzureServer.Core.Services.KeyVault.Models;

public class SecretDto
{
    public string Name { get; set; } = string.Empty;
    public string VaultName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? ContentType { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? CreatedOn { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public DateTime? NotBefore { get; set; }
    public string? Version { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public bool RecoveryLevel { get; set; }
    public int? RecoverableDays { get; set; }
}
