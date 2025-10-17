using AzureServer.Services.KeyVault;
using AzureServer.Services.KeyVault.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]/{vaultName}")]
public class KeyVaultController(IKeyVaultService keyVaultService, ILogger<KeyVaultController> logger) : ControllerBase
{
    [HttpGet("secrets")]
    public async Task<ActionResult> ListSecrets(string vaultName)
    {
        try
        {
            var secrets = await keyVaultService.ListSecretsAsync(vaultName);
            return Ok(new { success = true, secrets = secrets.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing secrets in vault {VaultName}", vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListSecrets", type = ex.GetType().Name });
        }
    }

    [HttpGet("secrets/{secretName}")]
    public async Task<ActionResult> GetSecret(string vaultName, string secretName, [FromQuery] string? version = null)
    {
        try
        {
            var secret = await keyVaultService.GetSecretAsync(vaultName, secretName, version);
            if (secret is null)
                return NotFound(new { success = false, error = $"Secret {secretName} not found in vault {vaultName}" });

            return Ok(new { success = true, secret });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetSecret", type = ex.GetType().Name });
        }
    }

    [HttpPost("secrets/{secretName}")]
    public async Task<ActionResult> SetSecret(
        string vaultName,
        string secretName,
        [FromBody] SetSecretRequest request)
    {
        try
        {
            DateTime? expiresOn = string.IsNullOrEmpty(request.ExpiresOn) ? null : DateTime.Parse(request.ExpiresOn);
            DateTime? notBefore = string.IsNullOrEmpty(request.NotBefore) ? null : DateTime.Parse(request.NotBefore);

            var secret = await keyVaultService.SetSecretAsync(
                vaultName, secretName, request.Value,
                request.ContentType, expiresOn, notBefore, request.Tags);

            return Ok(new { success = true, secret });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "SetSecret", type = ex.GetType().Name });
        }
    }

    [HttpDelete("secrets/{secretName}")]
    public async Task<ActionResult> DeleteSecret(string vaultName, string secretName)
    {
        try
        {
            var deletedSecret = await keyVaultService.DeleteSecretAsync(vaultName, secretName);
            return Ok(new { success = true, deletedSecret });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteSecret", type = ex.GetType().Name });
        }
    }

    [HttpGet("secrets/{secretName}/versions")]
    public async Task<ActionResult> GetSecretVersions(string vaultName, string secretName)
    {
        try
        {
            var versions = await keyVaultService.GetSecretVersionsAsync(vaultName, secretName);
            return Ok(new { success = true, versions = versions.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting versions for secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetSecretVersions", type = ex.GetType().Name });
        }
    }

    [HttpGet("deleted-secrets")]
    public async Task<ActionResult> ListDeletedSecrets(string vaultName)
    {
        try
        {
            var deletedSecrets = await keyVaultService.ListDeletedSecretsAsync(vaultName);
            return Ok(new { success = true, deletedSecrets = deletedSecrets.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing deleted secrets in vault {VaultName}", vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListDeletedSecrets", type = ex.GetType().Name });
        }
    }

    [HttpGet("deleted-secrets/{secretName}")]
    public async Task<ActionResult> GetDeletedSecret(string vaultName, string secretName)
    {
        try
        {
            var deletedSecret = await keyVaultService.GetDeletedSecretAsync(vaultName, secretName);
            if (deletedSecret is null)
                return NotFound(new { success = false, error = $"Deleted secret {secretName} not found in vault {vaultName}" });

            return Ok(new { success = true, deletedSecret });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting deleted secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetDeletedSecret", type = ex.GetType().Name });
        }
    }

    [HttpPost("deleted-secrets/{secretName}/recover")]
    public async Task<ActionResult> RecoverDeletedSecret(string vaultName, string secretName)
    {
        try
        {
            var properties = await keyVaultService.RecoverDeletedSecretAsync(vaultName, secretName);
            return Ok(new { success = true, properties });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recovering deleted secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "RecoverDeletedSecret", type = ex.GetType().Name });
        }
    }

    [HttpDelete("deleted-secrets/{secretName}/purge")]
    public async Task<ActionResult> PurgeDeletedSecret(string vaultName, string secretName)
    {
        try
        {
            await keyVaultService.PurgeDeletedSecretAsync(vaultName, secretName);
            return Ok(new { success = true, message = $"Secret {secretName} permanently purged from vault {vaultName}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purging deleted secret {SecretName} from vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "PurgeDeletedSecret", type = ex.GetType().Name });
        }
    }

    [HttpPatch("secrets/{secretName}/properties")]
    public async Task<ActionResult> UpdateSecretProperties(
        string vaultName,
        string secretName,
        [FromBody] UpdateSecretPropertiesRequest request)
    {
        try
        {
            DateTime? expiresOn = string.IsNullOrEmpty(request.ExpiresOn) ? null : DateTime.Parse(request.ExpiresOn);
            DateTime? notBefore = string.IsNullOrEmpty(request.NotBefore) ? null : DateTime.Parse(request.NotBefore);

            var properties = await keyVaultService.UpdateSecretPropertiesAsync(
                vaultName, secretName, request.Version, request.Enabled,
                expiresOn, notBefore, request.ContentType, request.Tags);

            return Ok(new { success = true, properties });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating properties for secret {SecretName} in vault {VaultName}", secretName, vaultName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "UpdateSecretProperties", type = ex.GetType().Name });
        }
    }
}

public record SetSecretRequest(
    string Value,
    string? ContentType = null,
    string? ExpiresOn = null,
    string? NotBefore = null,
    Dictionary<string, string>? Tags = null);

public record UpdateSecretPropertiesRequest(
    string? Version = null,
    bool? Enabled = null,
    string? ExpiresOn = null,
    string? NotBefore = null,
    string? ContentType = null,
    Dictionary<string, string>? Tags = null);