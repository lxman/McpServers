using AzureServer.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]/{accountName}/shares")]
public class FileStorageController(IFileStorageService fileStorageService, ILogger<FileStorageController> logger) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult> ListFileShares(string accountName, [FromQuery] string? prefix = null)
    {
        try
        {
            var shares = await fileStorageService.ListFileSharesAsync(accountName, prefix);
            return Ok(new { success = true, shares = shares.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing file shares in account {AccountName}", accountName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListFileShares", type = ex.GetType().Name });
        }
    }

    [HttpGet("{shareName}")]
    public async Task<ActionResult> GetFileShare(string accountName, string shareName)
    {
        try
        {
            var share = await fileStorageService.GetFileShareAsync(accountName, shareName);
            if (share is null)
                return NotFound(new { success = false, error = $"File share {shareName} not found" });

            return Ok(new { success = true, share });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file share {ShareName}", shareName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetFileShare", type = ex.GetType().Name });
        }
    }

    [HttpPost("{shareName}")]
    public async Task<ActionResult> CreateFileShare(
        string accountName,
        string shareName,
        [FromBody] CreateFileShareRequest? request = null)
    {
        try
        {
            var share = await fileStorageService.CreateFileShareAsync(
                accountName, shareName, request?.QuotaInGB, request?.Metadata);
            return Ok(new { success = true, share });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating file share {ShareName}", shareName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateFileShare", type = ex.GetType().Name });
        }
    }

    [HttpDelete("{shareName}")]
    public async Task<ActionResult> DeleteFileShare(string accountName, string shareName)
    {
        try
        {
            var deleted = await fileStorageService.DeleteFileShareAsync(accountName, shareName);
            return Ok(new { success = true, deleted });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting file share {ShareName}", shareName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteFileShare", type = ex.GetType().Name });
        }
    }

    [HttpHead("{shareName}")]
    public async Task<ActionResult> FileShareExists(string accountName, string shareName)
    {
        try
        {
            var exists = await fileStorageService.FileShareExistsAsync(accountName, shareName);
            return Ok(new { success = true, exists });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if file share {ShareName} exists", shareName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "FileShareExists", type = ex.GetType().Name });
        }
    }

    [HttpPost("{shareName}/directories")]
    public async Task<ActionResult> CreateDirectory(
        string accountName,
        string shareName,
        [FromBody] DirectoryRequest request)
    {
        try
        {
            var created = await fileStorageService.CreateDirectoryAsync(accountName, shareName, request.DirectoryPath);
            return Ok(new { success = true, created });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating directory {DirectoryPath}", request.DirectoryPath);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateDirectory", type = ex.GetType().Name });
        }
    }

    [HttpDelete("{shareName}/directories")]
    public async Task<ActionResult> DeleteDirectory(
        string accountName,
        string shareName,
        [FromQuery] string directoryPath)
    {
        try
        {
            var deleted = await fileStorageService.DeleteDirectoryAsync(accountName, shareName, directoryPath);
            return Ok(new { success = true, deleted });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting directory {DirectoryPath}", directoryPath);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "DeleteDirectory", type = ex.GetType().Name });
        }
    }
}

public record CreateFileShareRequest(int? QuotaInGB = null, Dictionary<string, string>? Metadata = null);
public record DirectoryRequest(string DirectoryPath);