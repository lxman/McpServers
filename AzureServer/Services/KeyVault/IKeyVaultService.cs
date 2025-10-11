using AzureServer.Services.KeyVault.Models;

namespace AzureServer.Services.KeyVault;

public interface IKeyVaultService
{
    /// <summary>
    /// Lists all secrets in the specified Key Vault (without values)
    /// </summary>
    Task<IEnumerable<SecretPropertiesDto>> ListSecretsAsync(string vaultName);

    /// <summary>
    /// Gets a secret value from the Key Vault
    /// </summary>
    Task<SecretDto?> GetSecretAsync(string vaultName, string secretName, string? version = null);

    /// <summary>
    /// Sets (creates or updates) a secret in the Key Vault
    /// </summary>
    Task<SecretDto> SetSecretAsync(string vaultName, string secretName, string value, string? contentType = null, 
        DateTime? expiresOn = null, DateTime? notBefore = null, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Deletes a secret from the Key Vault (soft delete)
    /// </summary>
    Task<DeletedSecretDto> DeleteSecretAsync(string vaultName, string secretName);

    /// <summary>
    /// Lists all versions of a specific secret
    /// </summary>
    Task<IEnumerable<SecretPropertiesDto>> GetSecretVersionsAsync(string vaultName, string secretName);

    /// <summary>
    /// Lists all deleted secrets (soft deleted, not yet purged)
    /// </summary>
    Task<IEnumerable<DeletedSecretDto>> ListDeletedSecretsAsync(string vaultName);

    /// <summary>
    /// Gets a deleted secret
    /// </summary>
    Task<DeletedSecretDto?> GetDeletedSecretAsync(string vaultName, string secretName);

    /// <summary>
    /// Recovers a soft-deleted secret
    /// </summary>
    Task<SecretPropertiesDto> RecoverDeletedSecretAsync(string vaultName, string secretName);

    /// <summary>
    /// Permanently deletes a soft-deleted secret (cannot be recovered)
    /// </summary>
    Task PurgeDeletedSecretAsync(string vaultName, string secretName);

    /// <summary>
    /// Updates secret properties (tags, expiration, etc.) without changing the value
    /// </summary>
    Task<SecretPropertiesDto> UpdateSecretPropertiesAsync(string vaultName, string secretName, string? version = null,
        bool? enabled = null, DateTime? expiresOn = null, DateTime? notBefore = null, 
        string? contentType = null, Dictionary<string, string>? tags = null);
}
