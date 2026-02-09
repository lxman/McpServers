using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using DocumentServer.Core.Services.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DocumentMcp.McpTools;

/// <summary>
/// MCP tools for password management operations
/// </summary>
[McpServerToolType]
public class PasswordTools(
    PasswordManager passwordManager,
    ILogger<PasswordTools> logger)
{
    [McpServerTool, DisplayName("register_password")]
    [Description("Register a password for a specific file. See skills/document/password/register.md only when using this tool")]
    public string RegisterPassword(string filePath, string password)
    {
        try
        {
            logger.LogDebug("Registering password for file: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Password is required" }, SerializerOptions.JsonOptionsIndented);
            }

            passwordManager.RegisterSpecificPassword(filePath, password);

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                message = "Password registered successfully"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering password for: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("register_password_pattern")]
    [Description("Register a password pattern for multiple files. See skills/document/password/register-pattern.md only when using this tool")]
    public string RegisterPasswordPattern(string pattern, string password)
    {
        try
        {
            logger.LogDebug("Registering password pattern: {Pattern}", pattern);

            if (string.IsNullOrWhiteSpace(pattern))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Pattern is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Password is required" }, SerializerOptions.JsonOptionsIndented);
            }

            passwordManager.RegisterPasswordPattern(pattern, password);

            return JsonSerializer.Serialize(new
            {
                success = true,
                pattern,
                message = "Password pattern registered successfully"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering password pattern: {Pattern}", pattern);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("register_bulk_passwords")]
    [Description("Register passwords for multiple files at once. See skills/document/password/register-bulk.md only when using this tool")]
    public string RegisterBulkPasswords(Dictionary<string, string> filePasswords)
    {
        try
        {
            logger.LogDebug("Registering bulk passwords for {Count} files", filePasswords?.Count ?? 0);

            if (filePasswords == null || filePasswords.Count == 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "File passwords dictionary is required" }, SerializerOptions.JsonOptionsIndented);
            }

            var successCount = 0;
            var errors = new List<object>();

            foreach (KeyValuePair<string, string> kvp in filePasswords)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        passwordManager.RegisterSpecificPassword(kvp.Key, kvp.Value);
                        successCount++;
                    }
                    else
                    {
                        errors.Add(new { file = kvp.Key, error = "Invalid file path or password" });
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new { file = kvp.Key, error = ex.Message });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                totalCount = filePasswords.Count,
                successCount,
                failedCount = errors.Count,
                errors = errors.Any() ? errors : null
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering bulk passwords");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("auto_detect_password")]
    [Description("Attempt to auto-detect password files in a directory tree. See skills/document/password/auto-detect.md only when using this tool")]
    public async Task<string> AutoDetectPassword(string rootPath)
    {
        try
        {
            logger.LogDebug("Auto-detecting password files in: {RootPath}", rootPath);

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Root path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            if (!Directory.Exists(rootPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "Directory not found" }, SerializerOptions.JsonOptionsIndented);
            }

            int detectedCount = await passwordManager.AutoDetectPasswordFilesAsync(rootPath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                rootPath,
                passwordFilesDetected = detectedCount,
                message = detectedCount > 0
                    ? $"Successfully detected and registered {detectedCount} password file(s)"
                    : "No password files detected in the directory tree"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error auto-detecting password files in: {RootPath}", rootPath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("check_password")]
    [Description("Check if a password is registered for a file. See skills/document/password/check.md only when using this tool")]
    public string CheckPassword(string filePath)
    {
        try
        {
            logger.LogDebug("Checking password for: {FilePath}", filePath);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "File path is required" }, SerializerOptions.JsonOptionsIndented);
            }

            bool hasPassword = passwordManager.HasPasswordForFile(filePath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                hasPassword
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking password for: {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_password_patterns")]
    [Description("Get all registered password patterns. See skills/document/password/get-patterns.md only when using this tool")]
    public string GetPasswordPatterns()
    {
        try
        {
            logger.LogDebug("Getting all password patterns");

            Dictionary<string, string> patterns = passwordManager.GetRegisteredPatterns();

            return JsonSerializer.Serialize(new
            {
                success = true,
                patternCount = patterns.Count,
                patterns = patterns.Select(p => new
                {
                    pattern = p.Key,
                    maskedPassword = p.Value
                })
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting password patterns");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_password_stats")]
    [Description("Get statistics about registered passwords. See skills/document/password/get-stats.md only when using this tool")]
    public string GetPasswordStats()
    {
        try
        {
            logger.LogDebug("Getting password statistics");

            int specificCount = passwordManager.GetSpecificPasswordCount();
            int patternCount = passwordManager.GetPatternCount();

            return JsonSerializer.Serialize(new
            {
                success = true,
                registeredFiles = specificCount,
                registeredPatterns = patternCount,
                totalRegistrations = specificCount + patternCount
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting password stats");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("clear_passwords")]
    [Description("Clear all registered passwords. See skills/document/password/clear.md only when using this tool")]
    public string ClearPasswords()
    {
        try
        {
            logger.LogDebug("Clearing all passwords");

            int specificCount = passwordManager.GetSpecificPasswordCount();
            int patternCount = passwordManager.GetPatternCount();
            int totalCount = specificCount + patternCount;

            passwordManager.ClearPasswords();

            return JsonSerializer.Serialize(new
            {
                success = true,
                clearedCount = totalCount,
                message = $"Cleared {totalCount} registered passwords and patterns"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing passwords");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}