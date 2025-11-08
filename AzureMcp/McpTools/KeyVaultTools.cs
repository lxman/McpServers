using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.KeyVault;
using AzureServer.Core.Services.KeyVault.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Key Vault operations
/// </summary>
[McpServerToolType]
public class KeyVaultTools(
    IKeyVaultService keyVaultService,
    ILogger<KeyVaultTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    #region Secret Operations

    [McpServerTool, DisplayName("list_secrets")]
    [Description("List secrets in Key Vault. See skills/azure/keyvault/list-secrets.md only when using this tool")]
    public async Task<string> ListSecrets(string vaultName)
    {
        try
        {
            logger.LogDebug("Listing secrets in vault {VaultName}", vaultName);
            IEnumerable<SecretPropertiesDto> secrets = await keyVaultService.ListSecretsAsync(vaultName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                secrets = secrets.Select(s => new
                {
                    name = s.Name,
                    enabled = s.Enabled,
                    createdOn = s.CreatedOn,
                    updatedOn = s.UpdatedOn,
                    expiresOn = s.ExpiresOn,
                    notBefore = s.NotBefore,
                    contentType = s.ContentType,
                    tags = s.Tags
                }).ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing secrets in vault {VaultName}", vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListSecrets",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_secret")]
    [Description("Get secret value from Key Vault. See skills/azure/keyvault/get-secret.md only when using this tool")]
    public async Task<string> GetSecret(string vaultName, string secretName, string? version = null)
    {
        try
        {
            logger.LogDebug("Getting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            SecretDto? secret = await keyVaultService.GetSecretAsync(vaultName, secretName, version);

            if (secret is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Secret {secretName} not found in vault {vaultName}"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                secret = new
                {
                    name = secret.Name,
                    value = secret.Value,
                    enabled = secret.Enabled,
                    createdOn = secret.CreatedOn,
                    updatedOn = secret.UpdatedOn,
                    expiresOn = secret.ExpiresOn,
                    notBefore = secret.NotBefore,
                    contentType = secret.ContentType,
                    tags = secret.Tags
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetSecret",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("set_secret")]
    [Description("Set secret value in Key Vault. See skills/azure/keyvault/set-secret.md only when using this tool")]
    public async Task<string> SetSecret(
        string vaultName,
        string secretName,
        string value,
        string? contentType = null,
        string? expiresOn = null,
        string? notBefore = null,
        Dictionary<string, string>? tags = null)
    {
        try
        {
            logger.LogDebug("Setting secret {SecretName} in vault {VaultName}", secretName, vaultName);

            DateTime? expiresOnDate = string.IsNullOrEmpty(expiresOn) ? null : DateTime.Parse(expiresOn);
            DateTime? notBeforeDate = string.IsNullOrEmpty(notBefore) ? null : DateTime.Parse(notBefore);

            SecretDto secret = await keyVaultService.SetSecretAsync(
                vaultName, secretName, value,
                contentType, expiresOnDate, notBeforeDate, tags);

            return JsonSerializer.Serialize(new
            {
                success = true,
                secret = new
                {
                    name = secret.Name,
                    value = secret.Value,
                    enabled = secret.Enabled,
                    createdOn = secret.CreatedOn,
                    updatedOn = secret.UpdatedOn,
                    expiresOn = secret.ExpiresOn,
                    notBefore = secret.NotBefore,
                    contentType = secret.ContentType,
                    tags = secret.Tags
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "SetSecret",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("delete_secret")]
    [Description("Delete secret from Key Vault. See skills/azure/keyvault/delete-secret.md only when using this tool")]
    public async Task<string> DeleteSecret(string vaultName, string secretName)
    {
        try
        {
            logger.LogDebug("Deleting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            DeletedSecretDto deletedSecret = await keyVaultService.DeleteSecretAsync(vaultName, secretName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                deletedSecret = new
                {
                    name = deletedSecret.Name,
                    deletedOn = deletedSecret.DeletedOn,
                    scheduledPurgeDate = deletedSecret.ScheduledPurgeDate,
                    recoveryId = deletedSecret.RecoveryId
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "DeleteSecret",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_secret_versions")]
    [Description("Get all versions of a secret. See skills/azure/keyvault/get-secret-versions.md only when using this tool")]
    public async Task<string> GetSecretVersions(string vaultName, string secretName)
    {
        try
        {
            logger.LogDebug("Getting versions for secret {SecretName} in vault {VaultName}", secretName, vaultName);
            IEnumerable<SecretPropertiesDto> versions = await keyVaultService.GetSecretVersionsAsync(vaultName, secretName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                versions = versions.Select(v => new
                {
                    name = v.Name,
                    version = v.Version,
                    enabled = v.Enabled,
                    createdOn = v.CreatedOn,
                    updatedOn = v.UpdatedOn,
                    expiresOn = v.ExpiresOn,
                    notBefore = v.NotBefore,
                    contentType = v.ContentType,
                    tags = v.Tags
                }).ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting versions for secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetSecretVersions",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    #endregion

    #region Deleted Secret Operations

    [McpServerTool, DisplayName("list_deleted_secrets")]
    [Description("List deleted secrets in Key Vault. See skills/azure/keyvault/list-deleted-secrets.md only when using this tool")]
    public async Task<string> ListDeletedSecrets(string vaultName)
    {
        try
        {
            logger.LogDebug("Listing deleted secrets in vault {VaultName}", vaultName);
            IEnumerable<DeletedSecretDto> deletedSecrets = await keyVaultService.ListDeletedSecretsAsync(vaultName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                deletedSecrets = deletedSecrets.Select(s => new
                {
                    name = s.Name,
                    deletedOn = s.DeletedOn,
                    scheduledPurgeDate = s.ScheduledPurgeDate,
                    recoveryId = s.RecoveryId
                }).ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing deleted secrets in vault {VaultName}", vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListDeletedSecrets",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_deleted_secret")]
    [Description("Get deleted secret from Key Vault. See skills/azure/keyvault/get-deleted-secret.md only when using this tool")]
    public async Task<string> GetDeletedSecret(string vaultName, string secretName)
    {
        try
        {
            logger.LogDebug("Getting deleted secret {SecretName} from vault {VaultName}", secretName, vaultName);
            DeletedSecretDto? deletedSecret = await keyVaultService.GetDeletedSecretAsync(vaultName, secretName);

            if (deletedSecret is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Deleted secret {secretName} not found in vault {vaultName}"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                deletedSecret = new
                {
                    name = deletedSecret.Name,
                    deletedOn = deletedSecret.DeletedOn,
                    scheduledPurgeDate = deletedSecret.ScheduledPurgeDate,
                    recoveryId = deletedSecret.RecoveryId
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting deleted secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetDeletedSecret",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("recover_deleted_secret")]
    [Description("Recover deleted secret in Key Vault. See skills/azure/keyvault/recover-deleted-secret.md only when using this tool")]
    public async Task<string> RecoverDeletedSecret(string vaultName, string secretName)
    {
        try
        {
            logger.LogDebug("Recovering deleted secret {SecretName} in vault {VaultName}", secretName, vaultName);
            SecretPropertiesDto properties = await keyVaultService.RecoverDeletedSecretAsync(vaultName, secretName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                properties = new
                {
                    name = properties.Name,
                    enabled = properties.Enabled,
                    createdOn = properties.CreatedOn,
                    updatedOn = properties.UpdatedOn,
                    expiresOn = properties.ExpiresOn,
                    notBefore = properties.NotBefore,
                    contentType = properties.ContentType,
                    tags = properties.Tags
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recovering deleted secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "RecoverDeletedSecret",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("purge_deleted_secret")]
    [Description("Permanently purge deleted secret from Key Vault. See skills/azure/keyvault/purge-deleted-secret.md only when using this tool")]
    public async Task<string> PurgeDeletedSecret(string vaultName, string secretName)
    {
        try
        {
            logger.LogDebug("Purging deleted secret {SecretName} from vault {VaultName}", secretName, vaultName);
            await keyVaultService.PurgeDeletedSecretAsync(vaultName, secretName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Secret {secretName} permanently purged from vault {vaultName}"
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purging deleted secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "PurgeDeletedSecret",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    #endregion

    #region Secret Properties Operations

    [McpServerTool, DisplayName("update_secret_properties")]
    [Description("Update secret properties in Key Vault. See skills/azure/keyvault/update-secret-properties.md only when using this tool")]
    public async Task<string> UpdateSecretProperties(
        string vaultName,
        string secretName,
        string? version = null,
        bool? enabled = null,
        string? expiresOn = null,
        string? notBefore = null,
        string? contentType = null,
        Dictionary<string, string>? tags = null)
    {
        try
        {
            logger.LogDebug("Updating properties for secret {SecretName} in vault {VaultName}", secretName, vaultName);

            DateTime? expiresOnDate = string.IsNullOrEmpty(expiresOn) ? null : DateTime.Parse(expiresOn);
            DateTime? notBeforeDate = string.IsNullOrEmpty(notBefore) ? null : DateTime.Parse(notBefore);

            SecretPropertiesDto properties = await keyVaultService.UpdateSecretPropertiesAsync(
                vaultName, secretName, version, enabled,
                expiresOnDate, notBeforeDate, contentType, tags);

            return JsonSerializer.Serialize(new
            {
                success = true,
                properties = new
                {
                    name = properties.Name,
                    enabled = properties.Enabled,
                    createdOn = properties.CreatedOn,
                    updatedOn = properties.UpdatedOn,
                    expiresOn = properties.ExpiresOn,
                    notBefore = properties.NotBefore,
                    contentType = properties.ContentType,
                    tags = properties.Tags
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating properties for secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "UpdateSecretProperties",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    #endregion
}
