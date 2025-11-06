using DocumentServer.Core.Models.Requests;
using DocumentServer.Core.Services.Core;
using Microsoft.AspNetCore.Mvc;

namespace DocumentServer.Controllers;

/// <summary>
/// Controller for managing document passwords
/// </summary>
[ApiController]
[Route("api/passwords")]
public class PasswordController(ILogger<PasswordController> logger, PasswordManager passwordManager)
    : ControllerBase
{
    /// <summary>
    /// Register a password for a specific document
    /// </summary>
    /// <param name="request">Password registration parameters</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult RegisterPassword([FromBody] RegisterPasswordRequest request)
    {
        logger.LogInformation("Registering password for document: {FilePath}", request.FilePath);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new { Error = "File path is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { Error = "Password is required" });
        }

        try
        {
            passwordManager.RegisterSpecificPassword(request.FilePath, request.Password);

            logger.LogInformation("Password registered successfully for: {FilePath}", request.FilePath);

            return Ok(new Dictionary<string, object>
            {
                ["success"] = true,
                ["filePath"] = request.FilePath,
                ["message"] = "Password registered successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering password for: {FilePath}", request.FilePath);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Register a password pattern for multiple files
    /// </summary>
    /// <param name="request">Password pattern parameters</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("pattern")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult RegisterPasswordPattern([FromBody] RegisterPasswordPatternRequest request)
    {
        logger.LogInformation("Registering password pattern: {Pattern}", request.Pattern);

        if (string.IsNullOrWhiteSpace(request.Pattern))
        {
            return BadRequest(new { Error = "Pattern is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { Error = "Password is required" });
        }

        try
        {
            passwordManager.RegisterPasswordPattern(request.Pattern, request.Password);

            logger.LogInformation("Password pattern registered successfully: {Pattern}", request.Pattern);

            return Ok(new Dictionary<string, object>
            {
                ["success"] = true,
                ["pattern"] = request.Pattern,
                ["message"] = "Password pattern registered successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering password pattern: {Pattern}", request.Pattern);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Register multiple passwords at once
    /// </summary>
    /// <param name="request">Bulk password registration parameters</param>
    /// <returns>Count of passwords registered</returns>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult BulkRegisterPasswords([FromBody] BulkRegisterPasswordsRequest request)
    {
        logger.LogInformation("Bulk registering {Count} passwords", request.PasswordMap.Count);

        if (request.PasswordMap.Count == 0)
        {
            return BadRequest(new { Error = "Password map is empty" });
        }

        try
        {
            int registered = passwordManager.BulkRegisterPasswords(request.PasswordMap);

            logger.LogInformation("Bulk password registration complete: {Count} registered", registered);

            return Ok(new Dictionary<string, object>
            {
                ["success"] = true,
                ["registered"] = registered,
                ["message"] = $"Registered {registered} passwords successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during bulk password registration");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Auto-detect and register passwords for encrypted files in a directory
    /// </summary>
    /// <param name="rootPath">Root directory to scan</param>
    /// <returns>Count of passwords detected and registered</returns>
    [HttpPost("auto-detect")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AutoDetectPasswords([FromQuery] string rootPath)
    {
        logger.LogInformation("Auto-detecting passwords in: {RootPath}", rootPath);

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return BadRequest(new { Error = "Root path is required" });
        }

        if (!Directory.Exists(rootPath))
        {
            logger.LogWarning("Directory not found: {RootPath}", rootPath);
            return BadRequest(new { Error = "Directory not found" });
        }

        try
        {
            int detected = await passwordManager.AutoDetectPasswordFilesAsync(rootPath);

            logger.LogInformation("Auto-detection complete: {Count} passwords detected", detected);

            return Ok(new Dictionary<string, object>
            {
                ["success"] = true,
                ["detected"] = detected,
                ["rootPath"] = rootPath,
                ["message"] = $"Detected and registered {detected} passwords"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during auto-detection: {RootPath}", rootPath);
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Check if a password is registered for a specific file
    /// </summary>
    /// <param name="filePath">File path to check</param>
    /// <returns>Whether a password is registered</returns>
    [HttpGet("check")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CheckPassword([FromQuery] string filePath)
    {
        logger.LogDebug("Checking if password exists for: {FilePath}", filePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { Error = "File path is required" });
        }

        bool hasPassword = passwordManager.HasPasswordForFile(filePath);

        return Ok(new Dictionary<string, object>
        {
            ["filePath"] = filePath,
            ["hasPassword"] = hasPassword
        });
    }

    /// <summary>
    /// Get all registered password patterns
    /// </summary>
    /// <returns>Dictionary of patterns and their passwords (passwords masked)</returns>
    [HttpGet("patterns")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult GetPatterns()
    {
        logger.LogDebug("Getting all registered password patterns");

        Dictionary<string, string> patterns = passwordManager.GetRegisteredPatterns();

        // Mask passwords for security
        Dictionary<string, string> maskedPatterns = patterns.ToDictionary(
            kvp => kvp.Key,
            kvp => new string('*', Math.Min(kvp.Value.Length, 8))
        );

        return Ok(new Dictionary<string, object>
        {
            ["patterns"] = maskedPatterns,
            ["count"] = patterns.Count
        });
    }

    /// <summary>
    /// Get statistics about registered passwords
    /// </summary>
    /// <returns>Password statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult GetStats()
    {
        logger.LogDebug("Getting password statistics");

        int specificCount = passwordManager.GetSpecificPasswordCount();
        int patternCount = passwordManager.GetPatternCount();

        return Ok(new Dictionary<string, object>
        {
            ["specificPasswords"] = specificCount,
            ["passwordPatterns"] = patternCount,
            ["totalRegistered"] = specificCount + patternCount
        });
    }

    /// <summary>
    /// Clear all registered passwords
    /// </summary>
    /// <returns>Success confirmation</returns>
    [HttpDelete("clear")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult ClearPasswords()
    {
        logger.LogWarning("Clearing all registered passwords");

        int specificCount = passwordManager.GetSpecificPasswordCount();
        int patternCount = passwordManager.GetPatternCount();
        int total = specificCount + patternCount;

        passwordManager.ClearPasswords();

        logger.LogInformation("Cleared all passwords: {Count} total", total);

        return Ok(new Dictionary<string, object>
        {
            ["success"] = true,
            ["cleared"] = total,
            ["message"] = $"Cleared {total} registered passwords"
        });
    }
}
