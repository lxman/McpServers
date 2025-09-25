using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class ConfigurationTools
{
    private readonly SecurityManager _securityManager;
    private readonly AuditLogger _auditLogger;

    public ConfigurationTools(SecurityManager securityManager, AuditLogger auditLogger)
    {
        _securityManager = securityManager;
        _auditLogger = auditLogger;
    }

    [McpServerTool]
    [Description("Get current security configuration")]
    public string GetConfiguration()
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("DesktopDriver Security Configuration:\n");
            
            result.AppendLine("Allowed Directories:");
            if (!_securityManager.AllowedDirectories.Any())
            {
                result.AppendLine("  * All directories allowed (no restrictions)");
            }
            else
            {
                foreach (var dir in _securityManager.AllowedDirectories)
                {
                    result.AppendLine($"  - {dir}");
                }
            }

            result.AppendLine("\nBlocked Commands:");
            if (!_securityManager.BlockedCommands.Any())
            {
                result.AppendLine("  * No commands blocked");
            }
            else
            {
                foreach (var cmd in _securityManager.BlockedCommands.OrderBy(x => x))
                {
                    result.AppendLine($"  - {cmd}");
                }
            }

            result.AppendLine("\nSystem Information:");
            result.AppendLine($"  User: {Environment.UserName}");
            result.AppendLine($"  Machine: {Environment.MachineName}");
            result.AppendLine($"  OS: {Environment.OSVersion}");
            result.AppendLine($"  Current Directory: {Environment.CurrentDirectory}");

            _auditLogger.LogOperation("Get_Configuration", "Configuration retrieved", true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperation("Get_Configuration", "Configuration retrieval", false, ex.Message);
            return $"Error getting configuration: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Add a directory to the allowed directories list")]
    public string AddAllowedDirectory(
        [Description("Directory path to allow")] string directoryPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            
            if (!Directory.Exists(fullPath))
            {
                var error = $"Directory does not exist: {fullPath}";
                _auditLogger.LogOperation("Add_Allowed_Directory", fullPath, false, error);
                return error;
            }

            _securityManager.AddAllowedDirectory(fullPath);
            _auditLogger.LogOperation("Add_Allowed_Directory", fullPath, true);
            return $"Directory added to allowed list: {fullPath}";
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperation("Add_Allowed_Directory", directoryPath, false, ex.Message);
            return $"Error adding allowed directory: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Add a command pattern to the blocked commands list")]
    public string AddBlockedCommand(
        [Description("Command pattern to block (case-insensitive)")] string commandPattern)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(commandPattern))
            {
                var error = "Command pattern cannot be empty";
                _auditLogger.LogOperation("Add_Blocked_Command", commandPattern, false, error);
                return error;
            }

            _securityManager.AddBlockedCommand(commandPattern);
            _auditLogger.LogOperation("Add_Blocked_Command", commandPattern, true);
            return $"Command pattern added to blocked list: {commandPattern}";
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperation("Add_Blocked_Command", commandPattern, false, ex.Message);
            return $"Error adding blocked command: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Test if a directory path is allowed")]
    public string TestDirectoryAccess(
        [Description("Directory path to test")] string directoryPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            var isAllowed = _securityManager.IsDirectoryAllowed(fullPath);
            
            var result = $"Directory Access Test:\n" +
                         $"Path: {fullPath}\n" +
                         $"Access: {(isAllowed ? "ALLOWED" : "DENIED")}\n";

            if (!isAllowed)
            {
                result += "\nTo allow access to this directory, use AddAllowedDirectory tool.";
            }

            _auditLogger.LogOperation("Test_Directory_Access", fullPath, true, $"Access: {(isAllowed ? "Allowed" : "Denied")}");
            return result;
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperation("Test_Directory_Access", directoryPath, false, ex.Message);
            return $"Error testing directory access: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Test if a command would be blocked")]
    public string TestCommandBlocking(
        [Description("Command to test")] string command)
    {
        try
        {
            var isBlocked = _securityManager.IsCommandBlocked(command);
            
            var result = $"Command Blocking Test:\n" +
                         $"Command: {command}\n" +
                         $"Status: {(isBlocked ? "BLOCKED" : "ALLOWED")}\n";

            if (isBlocked)
            {
                result += "\nThis command contains blocked patterns and will not be executed.";
            }

            _auditLogger.LogOperation("Test_Command_Blocking", command, true, $"Status: {(isBlocked ? "Blocked" : "Allowed")}");
            return result;
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperation("Test_Command_Blocking", command, false, ex.Message);
            return $"Error testing command blocking: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get help information about DesktopDriver tools and security")]
    public string GetHelp()
    {
        var help = @"DesktopDriver MCP Server - Help

SECURITY MODEL:
- Directory Access: Operations are restricted to allowed directories
- Command Blocking: Dangerous commands are blocked by pattern matching
- Audit Logging: All operations are logged for security monitoring

AVAILABLE TOOL CATEGORIES:

1. TERMINAL TOOLS:
   - ExecuteCommand: Run terminal commands with security checks
   - ReadProcessOutput: Read output from running processes
   - SendInput: Send input to interactive processes
   - KillProcess: Terminate processes by session ID
   - ListSessions: Show active terminal sessions

2. FILE SYSTEM TOOLS:
   - ReadFile: Read file contents with offset/length support
   - WriteFile: Write/append content to files
   - ListDirectory: List directory contents
   - CreateDirectory: Create directories
   - MoveFile: Move/rename files and directories
   - DeletePath: Delete files or directories
   - SearchFiles: Search for files by pattern
   - GetFileInfo: Get detailed file/directory information

3. PROCESS TOOLS:
   - ListProcesses: List running system processes
   - GetProcessInfo: Get detailed process information
   - KillProcessById: Terminate processes by PID
   - KillProcessesByName: Terminate all processes with a name
   - GetSystemInfo: Get system information and statistics

4. CONFIGURATION TOOLS:
   - GetConfiguration: View current security settings
   - AddAllowedDirectory: Add directory to allowed list
   - AddBlockedCommand: Add command pattern to blocked list
   - TestDirectoryAccess: Test if a directory is accessible
   - TestCommandBlocking: Test if a command would be blocked
   - GetHelp: Show this help information

SECURITY RECOMMENDATIONS:
- Keep allowed directories as restrictive as possible
- Regularly review blocked command patterns
- Monitor audit logs for suspicious activity
- Use confirmation parameters for destructive operations

For more information, check the audit logs or configuration files.";

        _auditLogger.LogOperation("Get_Help", "Help information provided", true);
        return help;
    }

    [McpServerTool]
    [Description("Get recent audit log entries")]
    public string GetAuditLog(
        [Description("Number of recent entries to retrieve")] int count = 20)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopDriver", "logs");
            var todayLogFile = Path.Combine(logDir, $"audit_{DateTime.Now:yyyyMMdd}.json");
            
            if (!File.Exists(todayLogFile))
            {
                return "No audit log found for today.";
            }

            var lines = File.ReadAllLines(todayLogFile);
            var recentLines = lines.TakeLast(count).ToArray();
            
            var result = new StringBuilder();
            result.AppendLine($"Recent Audit Log Entries (Last {recentLines.Length}):\n");
            
            foreach (var line in recentLines)
            {
                try
                {
                    // Parse and format the JSON for better readability
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    
                    var timestamp = root.GetProperty("Timestamp").GetDateTime().ToLocalTime();
                    var operation = root.GetProperty("Operation").GetString();
                    var success = root.GetProperty("Success").GetBoolean();
                    var details = root.GetProperty("Details").GetString();
                    
                    result.AppendLine($"{timestamp:yyyy-MM-dd HH:mm:ss} [{(success ? "SUCCESS" : "FAILED")}] {operation}");
                    result.AppendLine($"  Details: {details}");
                    
                    if (root.TryGetProperty("Error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
                    {
                        result.AppendLine($"  Error: {errorElement.GetString()}");
                    }
                    
                    result.AppendLine();
                }
                catch
                {
                    // If JSON parsing fails, just show the raw line
                    result.AppendLine(line);
                    result.AppendLine();
                }
            }

            _auditLogger.LogOperation("Get_Audit_Log", $"Retrieved {recentLines.Length} entries", true);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperation("Get_Audit_Log", "Audit log retrieval", false, ex.Message);
            return $"Error getting audit log: {ex.Message}";
        }
    }
}
