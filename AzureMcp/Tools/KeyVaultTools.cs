using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.KeyVault;
using AzureMcp.Services.KeyVault.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class KeyVaultTools(IKeyVaultService keyVaultService)
{
    #region Secret Tools

    [McpServerTool]
    [Description("List all secrets in a Key Vault (names and metadata only, no values)")]
    public async Task<string> ListSecretsAsync(
        [Description("Key Vault name (e.g., 'my-keyvault' for https://my-keyvault.vault.azure.net)")] string vaultName)
    {
        try
        {
            IEnumerable<SecretPropertiesDto> secrets = await keyVaultService.ListSecretsAsync(vaultName);
            return JsonSerializer.Serialize(new { success = true, secrets = secrets.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListSecrets");
        }
    }

    [McpServerTool]
    [Description("Get a secret value from Key Vault")]
    public async Task<string> GetSecretAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName,
        [Description("Optional specific version (defaults to latest)")] string? version = null)
    {
        try
        {
            SecretDto? secret = await keyVaultService.GetSecretAsync(vaultName, secretName, version);
            if (secret is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Secret {secretName} not found in vault {vaultName}" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, secret },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetSecret");
        }
    }

    [McpServerTool]
    [Description("Set (create or update) a secret in Key Vault")]
    public async Task<string> SetSecretAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName,
        [Description("Secret value")] string value,
        [Description("Optional content type (e.g., 'text/plain', 'application/json')")] string? contentType = null,
        [Description("Optional expiration date (ISO 8601 format)")] string? expiresOn = null,
        [Description("Optional not-before date (ISO 8601 format)")] string? notBefore = null,
        [Description("Optional tags as JSON object (e.g., '{\"Environment\":\"Production\"}')")] string? tagsJson = null)
    {
        try
        {
            DateTime? expiresOnDate = null;
            if (!string.IsNullOrEmpty(expiresOn))
                expiresOnDate = DateTime.Parse(expiresOn);

            DateTime? notBeforeDate = null;
            if (!string.IsNullOrEmpty(notBefore))
                notBeforeDate = DateTime.Parse(notBefore);

            Dictionary<string, string>? tags = null;
            if (!string.IsNullOrEmpty(tagsJson))
                tags = JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson);

            SecretDto secret = await keyVaultService.SetSecretAsync(vaultName, secretName, value, 
                contentType, expiresOnDate, notBeforeDate, tags);

            return JsonSerializer.Serialize(new { success = true, secret },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SetSecret");
        }
    }

    [McpServerTool]
    [Description("Delete a secret from Key Vault (soft delete - can be recovered)")]
    public async Task<string> DeleteSecretAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName)
    {
        try
        {
            DeletedSecretDto deletedSecret = await keyVaultService.DeleteSecretAsync(vaultName, secretName);
            return JsonSerializer.Serialize(new { success = true, deletedSecret },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteSecret");
        }
    }

    [McpServerTool]
    [Description("List all versions of a specific secret")]
    public async Task<string> GetSecretVersionsAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName)
    {
        try
        {
            IEnumerable<SecretPropertiesDto> versions = await keyVaultService.GetSecretVersionsAsync(vaultName, secretName);
            return JsonSerializer.Serialize(new { success = true, versions = versions.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetSecretVersions");
        }
    }

    [McpServerTool]
    [Description("List all deleted secrets in Key Vault (soft deleted, not yet purged)")]
    public async Task<string> ListDeletedSecretsAsync(
        [Description("Key Vault name")] string vaultName)
    {
        try
        {
            IEnumerable<DeletedSecretDto> deletedSecrets = await keyVaultService.ListDeletedSecretsAsync(vaultName);
            return JsonSerializer.Serialize(new { success = true, deletedSecrets = deletedSecrets.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListDeletedSecrets");
        }
    }

    [McpServerTool]
    [Description("Get a deleted secret from Key Vault")]
    public async Task<string> GetDeletedSecretAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName)
    {
        try
        {
            DeletedSecretDto? deletedSecret = await keyVaultService.GetDeletedSecretAsync(vaultName, secretName);
            if (deletedSecret is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Deleted secret {secretName} not found in vault {vaultName}" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, deletedSecret },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetDeletedSecret");
        }
    }

    [McpServerTool]
    [Description("Recover a soft-deleted secret in Key Vault")]
    public async Task<string> RecoverDeletedSecretAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName)
    {
        try
        {
            SecretPropertiesDto properties = await keyVaultService.RecoverDeletedSecretAsync(vaultName, secretName);
            return JsonSerializer.Serialize(new { success = true, properties },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "RecoverDeletedSecret");
        }
    }

    [McpServerTool]
    [Description("Permanently delete a soft-deleted secret (cannot be recovered after this)")]
    public async Task<string> PurgeDeletedSecretAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName)
    {
        try
        {
            await keyVaultService.PurgeDeletedSecretAsync(vaultName, secretName);
            return JsonSerializer.Serialize(new { success = true, message = $"Secret {secretName} permanently purged from vault {vaultName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "PurgeDeletedSecret");
        }
    }

    [McpServerTool]
    [Description("Update secret properties (tags, expiration, enabled status) without changing the value")]
    public async Task<string> UpdateSecretPropertiesAsync(
        [Description("Key Vault name")] string vaultName,
        [Description("Secret name")] string secretName,
        [Description("Optional specific version (defaults to latest)")] string? version = null,
        [Description("Optional enable/disable the secret")] bool? enabled = null,
        [Description("Optional expiration date (ISO 8601 format)")] string? expiresOn = null,
        [Description("Optional not-before date (ISO 8601 format)")] string? notBefore = null,
        [Description("Optional content type")] string? contentType = null,
        [Description("Optional tags as JSON object")] string? tagsJson = null)
    {
        try
        {
            DateTime? expiresOnDate = null;
            if (!string.IsNullOrEmpty(expiresOn))
                expiresOnDate = DateTime.Parse(expiresOn);

            DateTime? notBeforeDate = null;
            if (!string.IsNullOrEmpty(notBefore))
                notBeforeDate = DateTime.Parse(notBefore);

            Dictionary<string, string>? tags = null;
            if (!string.IsNullOrEmpty(tagsJson))
                tags = JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson);

            SecretPropertiesDto properties = await keyVaultService.UpdateSecretPropertiesAsync(
                vaultName, secretName, version, enabled, expiresOnDate, notBeforeDate, contentType, tags);

            return JsonSerializer.Serialize(new { success = true, properties },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateSecretProperties");
        }
    }

    #endregion

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = ex.Message,
            operation,
            type = ex.GetType().Name,
            stackTrace = ex.StackTrace
        }, SerializerOptions.JsonOptionsIndented);
    }
}
