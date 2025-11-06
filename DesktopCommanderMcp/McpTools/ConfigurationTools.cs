using System.ComponentModel;
using System.Text.Json;
using DesktopCommander.Core.Common;
using DesktopCommander.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for security configuration operations
/// </summary>
[McpServerToolType]
public class ConfigurationTools(
    SecurityManager securityManager,
    AuditLogger auditLogger,
    ILogger<ConfigurationTools> logger)
{
    [McpServerTool, DisplayName("get_security_configuration")]
    [Description("Get security config. See security-config/SKILL.md")]
    public Task<string> GetConfiguration()
    {
        try
        {
            var result = new
            {
                success = true,
                allowedDirectories = securityManager.AllowedDirectories,
                blockedCommands = securityManager.BlockedCommands
            };
            
            return Task.FromResult(JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting configuration");
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("add_allowed_directory")]
    [Description("Add directory to whitelist. See security-config/SKILL.md")]
    public Task<string> AddAllowedDirectory(
        string directoryPath)
    {
        try
        {
            securityManager.AddAllowedDirectory(directoryPath);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                directoryPath,
                message = "Directory added to allowed list"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding allowed directory: {Path}", directoryPath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("add_blocked_command")]
    [Description("Add command pattern to blocklist. See security-config/SKILL.md")]
    public Task<string> AddBlockedCommand(
        string commandPattern)
    {
        try
        {
            securityManager.AddBlockedCommand(commandPattern);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                commandPattern,
                message = "Command pattern added to blocked list"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding blocked command: {Pattern}", commandPattern);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("test_directory_access")]
    [Description("Test if directory is allowed. See security-config/SKILL.md")]
    public Task<string> TestDirectoryAccess(
        string directoryPath)
    {
        try
        {
            bool isAllowed = securityManager.IsDirectoryAllowed(directoryPath);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                directoryPath,
                isAllowed,
                message = isAllowed ? "Access allowed" : "Access denied"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing directory access: {Path}", directoryPath);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("test_command_blocking")]
    [Description("Test if command is blocked. See security-config/SKILL.md")]
    public Task<string> TestCommandBlocking(
        string command)
    {
        try
        {
            bool isAllowed = !securityManager.IsCommandBlocked(command);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                command,
                isAllowed,
                message = isAllowed ? "Command allowed" : "Command blocked"
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing command: {Command}", command);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("get_audit_log")]
    [Description("Get recent audit entries. See maintenance/SKILL.md")]
    public Task<string> GetAuditLog(
        int count = 20)
    {
        try
        {
            object result = auditLogger.GetRecentEntries(count);
            
            return Task.FromResult(JsonSerializer.Serialize(result, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting audit log");
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("get_desktop_commander_info")]
    [Description("Get help info and endpoints. See maintenance/SKILL.md")]
    public Task<string> GetHelp()
    {
        try
        {
            var help = new
            {
                success = true,
                version = "1.0.0",
                description = "DesktopCommander provides file system, document, process, and terminal operations via MCP",
                toolCategories = new
                {
                    fileSystem = "File operations (read, write, list, delete, move, search)",
                    fileReading = "Advanced file reading (ranges, chunks, context)",
                    fileEditing = "Line-based file editing with approval workflow",
                    documents = "Document indexing, search, and OCR",
                    processes = "Process management and monitoring",
                    terminal = "Command execution and session management",
                    hexAnalysis = "Binary file analysis and hex operations",
                    configuration = "Security configuration and audit logging",
                    services = "MCP service lifecycle management",
                    directory = "MCP server directory and HTTP tools"
                },
                security = new
                {
                    message = "Access to file paths and commands is controlled by security configuration",
                    managedVia = "Use add_allowed_directory and add_blocked_command tools"
                }
            };

            return Task.FromResult(JsonSerializer.Serialize(help, 
                SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting help");
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }
}