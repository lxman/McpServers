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
    [Description("Execute shell command. See command-execution/SKILL.md")]
    public async Task<string> ExecuteCommand(
        string command,
        string? workingDirectory = null,
        int timeoutMs = 30000,
        string sessionId = "default")
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
    [Description("Send input to persistent session. See command-execution/SKILL.md")]
    public async Task<string> SendInput(
        string sessionId,
        string input)
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
    [Description("Get output from persistent session. See command-execution/SKILL.md")]
    public Task<string> GetSessionOutput(
        string sessionId = "default")
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
    [Description("List active terminal sessions. See command-execution/SKILL.md")]
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
    [Description("Close persistent session. See command-execution/SKILL.md")]
    public Task<string> CloseSession(
        string sessionId)
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