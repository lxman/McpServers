using Microsoft.AspNetCore.Mvc;
using DesktopCommander.Services;

namespace DesktopCommander.Controllers;

/// <summary>
/// Security configuration operations API
/// </summary>
[ApiController]
[Route("api/config")]
public class ConfigurationController(
    SecurityManager securityManager,
    AuditLogger auditLogger,
    ILogger<ConfigurationController> logger) : ControllerBase
{
    /// <summary>
    /// Get the current security configuration
    /// </summary>
    [HttpGet("")]
    public IActionResult GetConfiguration()
    {
        try
        {
            var result = new
            {
                success = true,
                allowedDirectories = securityManager.AllowedDirectories,
                blockedCommands = securityManager.BlockedCommands
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting configuration");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Add a directory to the allowed directories list
    /// </summary>
    [HttpPost("allowed-directories")]
    public IActionResult AddAllowedDirectory([FromBody] AddDirectoryRequest request)
    {
        try
        {
            securityManager.AddAllowedDirectory(request.DirectoryPath);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding allowed directory: {Path}", request.DirectoryPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Add a command pattern to the blocked commands list
    /// </summary>
    [HttpPost("blocked-commands")]
    public IActionResult AddBlockedCommand([FromBody] AddCommandRequest request)
    {
        try
        {
            securityManager.AddBlockedCommand(request.CommandPattern);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding blocked command: {Pattern}", request.CommandPattern);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test if a directory path is allowed
    /// </summary>
    [HttpPost("test-directory-access")]
    public IActionResult TestDirectoryAccess([FromBody] TestDirectoryRequest request)
    {
        try
        {
            bool isAllowed = securityManager.IsDirectoryAllowed(request.DirectoryPath);
            return Ok(new
            {
                success = true,
                directoryPath = request.DirectoryPath,
                isAllowed,
                message = isAllowed ? "Access allowed" : "Access denied"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing directory access: {Path}", request.DirectoryPath);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test if a command is blocked
    /// </summary>
    [HttpPost("test-command-blocking")]
    public IActionResult TestCommandBlocking([FromBody] TestCommandRequest request)
    {
        try
        {
            bool isAllowed = !securityManager.IsCommandBlocked(request.Command);
            return Ok(new
            {
                success = true,
                command = request.Command,
                isAllowed,
                message = isAllowed ? "Command allowed" : "Command blocked"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing command: {Command}", request.Command);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent audit log entries
    /// </summary>
    [HttpGet("audit-log")]
    public IActionResult GetAuditLog([FromQuery] int count = 20)
    {
        try
        {
            object result = auditLogger.GetRecentEntries(count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting audit log");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get help information about DesktopCommander
    /// </summary>
    [HttpGet("help")]
    public IActionResult GetHelp()
    {
        try
        {
            var help = new
            {
                success = true,
                version = "1.0.0",
                description = "DesktopCommander provides file system, document, process, and terminal operations via REST API",
                endpoints = new
                {
                    fileSystem = "/api/filesystem",
                    fileEditing = "/api/editing",
                    fileReading = "/api/file-reading",
                    documents = "/api/documents",
                    processes = "/api/processes",
                    terminal = "/api/terminal",
                    configuration = "/api/config",
                    hexAnalysis = "/api/hex"
                },
                security = new
                {
                    message = "Access to file paths and commands is controlled by security configuration",
                    allowedDirectories = "Manage via /api/config/allowed-directories",
                    blockedCommands = "Manage via /api/config/blocked-commands"
                },
                documentation = "Access full OpenAPI spec at /description or /scalar/v1"
            };

            return Ok(help);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting help");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

// Request models
public record AddDirectoryRequest(string DirectoryPath);
public record AddCommandRequest(string CommandPattern);
public record TestDirectoryRequest(string DirectoryPath);
public record TestCommandRequest(string Command);