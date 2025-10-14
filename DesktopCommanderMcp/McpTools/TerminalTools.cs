using System.ComponentModel;
using System.Text.Json;
using DesktopCommanderMcp.Common;
using DesktopCommanderMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DesktopCommanderMcp.McpTools;

/// <summary>
/// MCP tools for terminal/command execution
/// </summary>
[McpServerToolType]
public class TerminalTools(
    ProcessManager processManager,
    SecurityManager securityManager,
    AuditLogger auditLogger,
    ILogger<TerminalTools> logger)
{
    [McpServerTool, DisplayName("execute_command")]
    [Description("Execute a shell command and return its output")]
    public async Task<string> ExecuteCommand(
        [Description("Command to execute")] string command,
        [Description("Working directory (optional)")] string? workingDirectory = null,
        [Description("Timeout in milliseconds (default: 30000)")] int timeoutMs = 30000,
        [Description("Session ID for persistent sessions (default: 'default')")] string sessionId = "default")
    {
        try
        {
            if (securityManager.IsCommandBlocked(command))
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = "Command is blocked",
                    command
                }, SerializerOptions.JsonOptionsIndented);
            }

            ProcessResult result = await processManager.StartProcessAsync(command, sessionId, timeoutMs, workingDirectory);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                exitCode = result.ExitCode,
                output = result.Output,
                error = result.Error,
                isTimeout = result.IsTimeout,
                isRunning = result.IsRunning,
                processId = result.ProcessId
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command: {Command}", command);
            return JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("send_session_input")]
    [Description("Send input to a persistent terminal session")]
    public async Task<string> SendInput(
        [Description("Session ID")] string sessionId,
        [Description("Input text to send")] string input)
    {
        try
        {
            bool success = await processManager.SendInputAsync(sessionId, input);
            
            if (!success)
            {
                return JsonSerializer.Serialize(
                    new { success = false, error = "Session not found or process has exited", sessionId }, 
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                sessionId,
                inputSent = input
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending input to session {SessionId}", sessionId);
            return JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_session_output")]
    [Description("Get output from a persistent terminal session")]
    public Task<string> GetSessionOutput(
        [Description("Session ID (default: 'default')")] string sessionId = "default")
    {
        try
        {
            ProcessResult? result = processManager.GetProcessOutput(sessionId);
            
            if (result == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Session not found", sessionId }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                sessionId,
                output = result.Output,
                error = result.Error,
                isRunning = result.IsRunning,
                exitCode = result.ExitCode,
                processId = result.ProcessId
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting session output for {SessionId}", sessionId);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("list_terminal_sessions")]
    [Description("List all active terminal sessions")]
    public Task<string> ListSessions()
    {
        try
        {
            List<ProcessInfo> sessions = processManager.ListActiveSessions();
            
            var sessionList = sessions.Select(s => new
            {
                sessionId = s.SessionId,
                processId = s.ProcessId,
                command = s.Command,
                startTime = s.StartTime,
                isRunning = s.IsRunning,
                exitCode = s.ExitCode
            }).ToArray();

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                sessionCount = sessionList.Length,
                sessions = sessionList
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing sessions");
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }

    [McpServerTool, DisplayName("close_terminal_session")]
    [Description("Close a persistent terminal session")]
    public Task<string> CloseSession(
        [Description("Session ID")] string sessionId)
    {
        try
        {
            bool success = processManager.KillProcess(sessionId);
            
            if (!success)
            {
                return Task.FromResult(JsonSerializer.Serialize(
                    new { success = false, error = "Session not found", sessionId }, 
                    SerializerOptions.JsonOptionsIndented));
            }

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                sessionId,
                closed = true
            }, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error closing session {SessionId}", sessionId);
            return Task.FromResult(JsonSerializer.Serialize(
                new { success = false, error = ex.Message }, 
                SerializerOptions.JsonOptionsIndented));
        }
    }
}