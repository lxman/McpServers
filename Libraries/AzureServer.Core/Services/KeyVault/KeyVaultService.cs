using Azure;
using Azure.Security.KeyVault.Secrets;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.KeyVault.Models;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.KeyVault;

public class KeyVaultService(
    ArmClientFactory armClientFactory,
    ILogger<KeyVaultService> logger) : IKeyVaultService
{
    private readonly Dictionary<string, SecretClient> _secretClients = new();

    private async Task<SecretClient> GetSecretClientAsync(string vaultName)
    {
        if (_secretClients.TryGetValue(vaultName, out var existingClient))
            return existingClient;

        var vaultUri = new Uri($"https://{vaultName}.vault.azure.net");
        var client = new SecretClient(vaultUri, await armClientFactory.GetCredentialAsync());
        _secretClients[vaultName] = client;

        return client;
    }

    #region Secret Operations

    public async Task<IEnumerable<SecretPropertiesDto>> ListSecretsAsync(string vaultName)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            var secrets = new List<SecretPropertiesDto>();

            await foreach (var secret in client.GetPropertiesOfSecretsAsync())
            {
                secrets.Add(MapSecretProperties(secret, vaultName));
            }

            logger.LogInformation("Retrieved {Count} secrets from vault {VaultName}", secrets.Count, vaultName);
            return secrets;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing secrets in vault {VaultName}", vaultName);
            throw;
        }
    }

    public async Task<SecretDto?> GetSecretAsync(string vaultName, string secretName, string? version = null)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            
            KeyVaultSecret secret = string.IsNullOrEmpty(version)
                ? await client.GetSecretAsync(secretName)
                : await client.GetSecretAsync(secretName, version);

            logger.LogInformation("Retrieved secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return MapSecret(secret, vaultName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Secret {SecretName} not found in vault {VaultName}", secretName, vaultName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            throw;
        }
    }

    public async Task<SecretDto> SetSecretAsync(string vaultName, string secretName, string value, 
        string? contentType = null, DateTime? expiresOn = null, DateTime? notBefore = null, 
        Dictionary<string, string>? tags = null)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            var secret = new KeyVaultSecret(secretName, value);

            if (!string.IsNullOrEmpty(contentType))
                secret.Properties.ContentType = contentType;

            if (expiresOn.HasValue)
                secret.Properties.ExpiresOn = expiresOn.Value;

            if (notBefore.HasValue)
                secret.Properties.NotBefore = notBefore.Value;

            if (tags is not null)
            {
                foreach (var tag in tags)
                {
                    secret.Properties.Tags[tag.Key] = tag.Value;
                }
            }

            KeyVaultSecret result = await client.SetSecretAsync(secret);

            logger.LogInformation("Set secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return MapSecret(result, vaultName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting secret {SecretName} in vault {VaultName}", secretName, vaultName);
            throw;
        }
    }

    public async Task<DeletedSecretDto> DeleteSecretAsync(string vaultName, string secretName)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            var operation = await client.StartDeleteSecretAsync(secretName);
            DeletedSecret deletedSecret = await operation.WaitForCompletionAsync();

            logger.LogInformation("Deleted secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return MapDeletedSecret(deletedSecret, vaultName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            throw;
        }
    }

    public async Task<IEnumerable<SecretPropertiesDto>> GetSecretVersionsAsync(string vaultName, string secretName)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            var versions = new List<SecretPropertiesDto>();

            await foreach (var version in client.GetPropertiesOfSecretVersionsAsync(secretName))
            {
                versions.Add(MapSecretProperties(version, vaultName));
            }

            logger.LogInformation("Retrieved {Count} versions of secret {SecretName} from vault {VaultName}", 
                versions.Count, secretName, vaultName);
            return versions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting versions of secret {SecretName} from vault {VaultName}", 
                secretName, vaultName);
            throw;
        }
    }

    public async Task<IEnumerable<DeletedSecretDto>> ListDeletedSecretsAsync(string vaultName)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            var deletedSecrets = new List<DeletedSecretDto>();

            await foreach (var secret in client.GetDeletedSecretsAsync())
            {
                deletedSecrets.Add(MapDeletedSecret(secret, vaultName));
            }

            logger.LogInformation("Retrieved {Count} deleted secrets from vault {VaultName}", 
                deletedSecrets.Count, vaultName);
            return deletedSecrets;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing deleted secrets in vault {VaultName}", vaultName);
            throw;
        }
    }

    public async Task<DeletedSecretDto?> GetDeletedSecretAsync(string vaultName, string secretName)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            DeletedSecret deletedSecret = await client.GetDeletedSecretAsync(secretName);

            logger.LogInformation("Retrieved deleted secret {SecretName} from vault {VaultName}", 
                secretName, vaultName);
            return MapDeletedSecret(deletedSecret, vaultName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Deleted secret {SecretName} not found in vault {VaultName}", secretName, vaultName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting deleted secret {SecretName} from vault {VaultName}", 
                secretName, vaultName);
            throw;
        }
    }

    public async Task<SecretPropertiesDto> RecoverDeletedSecretAsync(string vaultName, string secretName)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            var operation = await client.StartRecoverDeletedSecretAsync(secretName);
            SecretProperties properties = await operation.WaitForCompletionAsync();

            logger.LogInformation("Recovered deleted secret {SecretName} in vault {VaultName}", 
                secretName, vaultName);
            return MapSecretProperties(properties, vaultName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recovering deleted secret {SecretName} in vault {VaultName}", 
                secretName, vaultName);
            throw;
        }
    }

    public async Task PurgeDeletedSecretAsync(string vaultName, string secretName)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            await client.PurgeDeletedSecretAsync(secretName);

            logger.LogInformation("Purged deleted secret {SecretName} from vault {VaultName}", 
                secretName, vaultName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purging deleted secret {SecretName} from vault {VaultName}", 
                secretName, vaultName);
            throw;
        }
    }

    public async Task<SecretPropertiesDto> UpdateSecretPropertiesAsync(string vaultName, string secretName, 
        string? version = null, bool? enabled = null, DateTime? expiresOn = null, DateTime? notBefore = null, 
        string? contentType = null, Dictionary<string, string>? tags = null)
    {
        try
        {
            var client = await GetSecretClientAsync(vaultName);
            
            var properties = string.IsNullOrEmpty(version)
                ? (await client.GetSecretAsync(secretName)).Value.Properties
                : (await client.GetSecretAsync(secretName, version)).Value.Properties;

            if (enabled.HasValue)
                properties.Enabled = enabled.Value;

            if (expiresOn.HasValue)
                properties.ExpiresOn = expiresOn.Value;

            if (notBefore.HasValue)
                properties.NotBefore = notBefore.Value;

            if (!string.IsNullOrEmpty(contentType))
                properties.ContentType = contentType;

            if (tags is not null)
            {
                properties.Tags.Clear();
                foreach (var tag in tags)
                {
                    properties.Tags[tag.Key] = tag.Value;
                }
            }

            SecretProperties updatedProperties = await client.UpdateSecretPropertiesAsync(properties);

            logger.LogInformation("Updated properties of secret {SecretName} in vault {VaultName}", 
                secretName, vaultName);
            return MapSecretProperties(updatedProperties, vaultName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating properties of secret {SecretName} in vault {VaultName}", 
                secretName, vaultName);
            throw;
        }
    }

    #endregion

    #region Mapping Methods

    private static SecretDto MapSecret(KeyVaultSecret secret, string vaultName)
    {
        return new SecretDto
        {
            Name = secret.Name,
            VaultName = vaultName,
            Value = secret.Value,
            ContentType = secret.Properties.ContentType,
            Enabled = secret.Properties.Enabled ?? true,
            CreatedOn = secret.Properties.CreatedOn?.DateTime,
            UpdatedOn = secret.Properties.UpdatedOn?.DateTime,
            ExpiresOn = secret.Properties.ExpiresOn?.DateTime,
            NotBefore = secret.Properties.NotBefore?.DateTime,
            Version = secret.Properties.Version,
            Tags = secret.Properties.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            RecoveryLevel = secret.Properties.RecoveryLevel is not null,
            RecoverableDays = secret.Properties.RecoverableDays
        };
    }

    private static SecretPropertiesDto MapSecretProperties(SecretProperties properties, string vaultName)
    {
        return new SecretPropertiesDto
        {
            Name = properties.Name,
            VaultName = vaultName,
            ContentType = properties.ContentType,
            Enabled = properties.Enabled ?? true,
            CreatedOn = properties.CreatedOn?.DateTime,
            UpdatedOn = properties.UpdatedOn?.DateTime,
            ExpiresOn = properties.ExpiresOn?.DateTime,
            NotBefore = properties.NotBefore?.DateTime,
            Version = properties.Version,
            Tags = properties.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Managed = properties.Managed,
            RecoveryLevel = properties.RecoveryLevel,
            RecoverableDays = properties.RecoverableDays
        };
    }

    private static DeletedSecretDto MapDeletedSecret(DeletedSecret secret, string vaultName)
    {
        return new DeletedSecretDto
        {
            Name = secret.Name,
            VaultName = vaultName,
            Value = secret.Value,
            DeletedOn = secret.DeletedOn?.DateTime,
            ScheduledPurgeDate = secret.ScheduledPurgeDate?.DateTime,
            RecoveryId = secret.RecoveryId.ToString(),
            Properties = MapSecretProperties(secret.Properties, vaultName)
        };
    }

    #endregion
}
