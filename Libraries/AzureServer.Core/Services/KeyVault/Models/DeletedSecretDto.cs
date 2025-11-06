namespace AzureServer.Core.Services.KeyVault.Models;

public class DeletedSecretDto
{
    public string Name { get; set; } = string.Empty;
    public string VaultName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime? DeletedOn { get; set; }
    public DateTime? ScheduledPurgeDate { get; set; }
    public string? RecoveryId { get; set; }
    public SecretPropertiesDto? Properties { get; set; }
}
