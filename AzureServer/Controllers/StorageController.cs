using AzureServer.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StorageController(IStorageService storageService, ILogger<StorageController> logger) : ControllerBase
{
    // Storage Account Endpoints
    [HttpGet("accounts")]
    public async Task<ActionResult> ListStorageAccounts([FromQuery] string? subscriptionId = null)
    {
        try
        {
            var accounts = await storageService.ListStorageAccountsAsync(subscriptionId);
            return Ok(new { success = true, storageAccounts = accounts.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing storage accounts");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListStorageAccounts", type = ex.GetType().Name });
        }
    }

    [HttpGet("accounts/{subscriptionId}/{resourceGroupName}/{accountName}")]
    public async Task<ActionResult> GetStorageAccount(string subscriptionId, string resourceGroupName, string accountName)
    {
        try
        {
            var account = await storageService.GetStorageAccountAsync(subscriptionId, resourceGroupName, accountName);
            if (account is null)
                return NotFound(new { success = false, error = $"Storage account {accountName} not found" });

            return Ok(new { success = true, storageAccount = account });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting storage account {AccountName}", accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetStorageAccount", type = ex.GetType().Name });
        }
    }

    // Container Endpoints
    [HttpGet("{accountName}/containers")]
    public async Task<ActionResult> ListContainers(string accountName, [FromQuery] string? prefix = null)
    {
        try
        {
            var containers = await storageService.ListContainersAsync(accountName, prefix);
            return Ok(new { success = true, containers = containers.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing containers in account {AccountName}", accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListContainers", type = ex.GetType().Name });
        }
    }

    [HttpGet("{accountName}/containers/{containerName}")]
    public async Task<ActionResult> GetContainer(string accountName, string containerName)
    {
        try
        {
            var container = await storageService.GetContainerAsync(accountName, containerName);
            if (container is null)
                return NotFound(new { success = false, error = $"Container {containerName} not found" });

            return Ok(new { success = true, container });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting container {ContainerName} in account {AccountName}", containerName, accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetContainer", type = ex.GetType().Name });
        }
    }

    [HttpPost("{accountName}/containers/{containerName}")]
    public async Task<ActionResult> CreateContainer(
        string accountName,
        string containerName,
        [FromBody] CreateContainerRequest? request = null)
    {
        try
        {
            var container = await storageService.CreateContainerAsync(accountName, containerName, request?.Metadata);
            return Ok(new { success = true, container });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating container {ContainerName} in account {AccountName}", containerName, accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateContainer", type = ex.GetType().Name });
        }
    }

    [HttpDelete("{accountName}/containers/{containerName}")]
    public async Task<ActionResult> DeleteContainer(string accountName, string containerName)
    {
        try
        {
            var deleted = await storageService.DeleteContainerAsync(accountName, containerName);
            return Ok(new { success = true, deleted, message = deleted ? "Container deleted successfully" : "Container not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting container {ContainerName} from account {AccountName}", containerName, accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteContainer", type = ex.GetType().Name });
        }
    }

    [HttpHead("{accountName}/containers/{containerName}")]
    public async Task<ActionResult> ContainerExists(string accountName, string containerName)
    {
        try
        {
            var exists = await storageService.ContainerExistsAsync(accountName, containerName);
            return Ok(new { success = true, exists });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if container {ContainerName} exists in account {AccountName}", containerName, accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ContainerExists", type = ex.GetType().Name });
        }
    }

    // Blob Endpoints
    [HttpGet("{accountName}/containers/{containerName}/blobs")]
    public async Task<ActionResult> ListBlobs(
        string accountName,
        string containerName,
        [FromQuery] string? prefix = null,
        [FromQuery] int? maxResults = null)
    {
        try
        {
            var blobs = await storageService.ListBlobsAsync(accountName, containerName, prefix, maxResults);
            return Ok(new { success = true, blobs = blobs.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing blobs in container {ContainerName} in account {AccountName}", containerName, accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListBlobs", type = ex.GetType().Name });
        }
    }

    [HttpGet("{accountName}/containers/{containerName}/blobs/{blobName}/properties")]
    public async Task<ActionResult> GetBlobProperties(string accountName, string containerName, string blobName)
    {
        try
        {
            var properties = await storageService.GetBlobPropertiesAsync(accountName, containerName, blobName);
            if (properties is null)
                return NotFound(new { success = false, error = $"Blob {blobName} not found" });

            return Ok(new { success = true, blobProperties = properties });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting properties for blob {BlobName}", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBlobProperties", type = ex.GetType().Name });
        }
    }

    [HttpGet("{accountName}/containers/{containerName}/blobs/{blobName}/content")]
    public async Task<ActionResult> DownloadBlobAsText(string accountName, string containerName, string blobName)
    {
        try
        {
            var content = await storageService.DownloadBlobAsTextAsync(accountName, containerName, blobName);
            return Ok(new { success = true, content, blobName, containerName, accountName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading blob {BlobName} as text", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DownloadBlobAsText", type = ex.GetType().Name });
        }
    }

    [HttpPost("{accountName}/containers/{containerName}/blobs/{blobName}/content")]
    public async Task<ActionResult> UploadBlobFromText(
        string accountName,
        string containerName,
        string blobName,
        [FromBody] UploadBlobRequest request)
    {
        try
        {
            var blob = await storageService.UploadBlobFromTextAsync(
                accountName, containerName, blobName, request.Content, request.ContentType);
            return Ok(new { success = true, blob, message = "Blob uploaded successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading blob {BlobName}", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "UploadBlobFromText", type = ex.GetType().Name });
        }
    }

    [HttpDelete("{accountName}/containers/{containerName}/blobs/{blobName}")]
    public async Task<ActionResult> DeleteBlob(string accountName, string containerName, string blobName)
    {
        try
        {
            var deleted = await storageService.DeleteBlobAsync(accountName, containerName, blobName);
            return Ok(new { success = true, deleted, message = deleted ? "Blob deleted successfully" : "Blob not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting blob {BlobName}", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteBlob", type = ex.GetType().Name });
        }
    }

    [HttpHead("{accountName}/containers/{containerName}/blobs/{blobName}")]
    public async Task<ActionResult> BlobExists(string accountName, string containerName, string blobName)
    {
        try
        {
            var exists = await storageService.BlobExistsAsync(accountName, containerName, blobName);
            return Ok(new { success = true, exists });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if blob {BlobName} exists", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "BlobExists", type = ex.GetType().Name });
        }
    }

    [HttpPost("copy-blob")]
    public async Task<ActionResult> CopyBlob([FromBody] CopyBlobRequest request)
    {
        try
        {
            var blob = await storageService.CopyBlobAsync(
                request.SourceAccountName, request.SourceContainerName, request.SourceBlobName,
                request.DestAccountName, request.DestContainerName, request.DestBlobName);

            return Ok(new
            {
                success = true,
                blob,
                message = "Blob copied successfully",
                source = $"{request.SourceAccountName}/{request.SourceContainerName}/{request.SourceBlobName}",
                destination = $"{request.DestAccountName}/{request.DestContainerName}/{request.DestBlobName}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error copying blob");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CopyBlob", type = ex.GetType().Name });
        }
    }

    // Blob Metadata Endpoints
    [HttpGet("{accountName}/containers/{containerName}/blobs/{blobName}/metadata")]
    public async Task<ActionResult> GetBlobMetadata(string accountName, string containerName, string blobName)
    {
        try
        {
            var metadata = await storageService.GetBlobMetadataAsync(accountName, containerName, blobName);
            return Ok(new { success = true, metadata });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting metadata for blob {BlobName}", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBlobMetadata", type = ex.GetType().Name });
        }
    }

    [HttpPut("{accountName}/containers/{containerName}/blobs/{blobName}/metadata")]
    public async Task<ActionResult> SetBlobMetadata(
        string accountName,
        string containerName,
        string blobName,
        [FromBody] Dictionary<string, string> metadata)
    {
        try
        {
            await storageService.SetBlobMetadataAsync(accountName, containerName, blobName, metadata);
            return Ok(new { success = true, message = "Metadata set successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting metadata for blob {BlobName}", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "SetBlobMetadata", type = ex.GetType().Name });
        }
    }

    // SAS Token Endpoints
    [HttpPost("{accountName}/containers/{containerName}/blobs/{blobName}/sas")]
    public async Task<ActionResult> GenerateBlobSasUrl(
        string accountName,
        string containerName,
        string blobName,
        [FromBody] GenerateSasRequest request)
    {
        try
        {
            var sasToken = await storageService.GenerateBlobSasUrlAsync(
                accountName, containerName, blobName, request.ExpirationHours, request.Permissions);
            return Ok(new { success = true, sasToken });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for blob {BlobName}", blobName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GenerateBlobSasUrl", type = ex.GetType().Name });
        }
    }

    [HttpPost("{accountName}/containers/{containerName}/sas")]
    public async Task<ActionResult> GenerateContainerSasUrl(
        string accountName,
        string containerName,
        [FromBody] GenerateSasRequest request)
    {
        try
        {
            var sasToken = await storageService.GenerateContainerSasUrlAsync(
                accountName, containerName, request.ExpirationHours, request.Permissions);
            return Ok(new { success = true, sasToken });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SAS URL for container {ContainerName}", containerName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GenerateContainerSasUrl", type = ex.GetType().Name });
        }
    }
}

public record CreateContainerRequest(Dictionary<string, string>? Metadata = null);
public record UploadBlobRequest(string Content, string? ContentType = null);
public record CopyBlobRequest(
    string SourceAccountName,
    string SourceContainerName,
    string SourceBlobName,
    string DestAccountName,
    string DestContainerName,
    string DestBlobName);
public record GenerateSasRequest(int ExpirationHours = 1, string Permissions = "r");