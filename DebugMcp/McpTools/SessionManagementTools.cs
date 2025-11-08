using System.ComponentModel;
using System.Text.Json;
using DebugServer.Core.Common;
using DebugServer.Core.Models;
using DebugServer.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.McpTools;

/// <summary>
/// MCP tools for managing debug sessions
/// </summary>
[McpServerToolType]
public class SessionManagementTools(
    DebuggerSessionManager sessionManager, 
    MiClient miClient,
    ILogger<SessionManagementTools> logger)
{
    [McpServerTool, DisplayName("debug_launch")]
    [Description("See skills/debug/session/debug_launch.md only when using this tool")]
    public async Task<string> LaunchAsync(
        string executablePath,
        string? workingDirectory = null,
        string? arguments = null)
    {
        try
        {
            logger.LogInformation("Creating debug session for {Executable}", executablePath);

            if (!File.Exists(executablePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Executable not found: {executablePath}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse arguments string into an array if provided
            string[]? argumentsArray = null;
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                argumentsArray = ParseArguments(arguments);
            }

            // Launch debug session using MiClient
            var sessionId = await miClient.LaunchAsync(
                executablePath,
                workingDirectory,
                argumentsArray);

            // Create and register session
            var session = new DebugSession
            {
                SessionId = sessionId,
                ExecutablePath = executablePath,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                Arguments = arguments,
                CreatedAt = DateTime.UtcNow,
                State = DebugSessionState.Running
            };

            sessionManager.RegisterSession(session);

            logger.LogInformation("Debug session {SessionId} created successfully", sessionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                sessionId = session.SessionId,
                state = session.State.ToString(),
                executablePath = session.ExecutablePath,
                workingDirectory = session.WorkingDirectory,
                message = $"Debug session {session.SessionId} started successfully"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error launching debug session");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_stop")]
    [Description("See skills/debug/session/debug_stop.md only when using this tool")]
    public async Task<string> StopAsync(
        string sessionId)
    {
        try
        {
            logger.LogInformation("Stopping debug session {SessionId}", sessionId);

            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Session not found: {sessionId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Disconnect from debugger
            await miClient.DisconnectAsync(sessionId);

            // Update session state
            session.State = DebugSessionState.Terminated;

            // Remove from session manager
            sessionManager.RemoveSession(sessionId);

            logger.LogInformation("Debug session {SessionId} stopped successfully", sessionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                sessionId,
                message = $"Debug session {sessionId} stopped"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping debug session {SessionId}", sessionId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_list_sessions")]
    [Description("See skills/debug/session/debug_list_sessions.md only when using this tool")]
    public string ListSessions()
    {
        try
        {
            var allSessions = sessionManager.GetAllSessions();
            var activeSessions = miClient.GetActiveSessions();

            var sessions = allSessions.Select(s => new
            {
                sessionId = s.SessionId,
                state = s.State.ToString(),
                isActive = activeSessions.Contains(s.SessionId),
                executablePath = s.ExecutablePath,
                executableName = Path.GetFileName(s.ExecutablePath),
                workingDirectory = s.WorkingDirectory,
                createdAt = s.CreatedAt,
                breakpointCount = s.Breakpoints.Count
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = sessions.Count,
                activeCount = sessions.Count(s => s.isActive),
                sessions
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing sessions");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_get_session_info")]
    [Description("See skills/debug/session/debug_get_session_info.md only when using this tool")]
    public string GetSessionInfo(
        string sessionId)
    {
        try
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Session not found: {sessionId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            var activeSessions = miClient.GetActiveSessions();
            var isActive = activeSessions.Contains(sessionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                sessionId = session.SessionId,
                state = session.State.ToString(),
                isActive,
                executablePath = session.ExecutablePath,
                executableName = Path.GetFileName(session.ExecutablePath),
                workingDirectory = session.WorkingDirectory,
                arguments = session.Arguments,
                createdAt = session.CreatedAt,
                uptime = DateTime.UtcNow - session.CreatedAt,
                breakpointCount = session.Breakpoints.Count,
                breakpoints = session.Breakpoints.Select(b => new
                {
                    id = b.Id,
                    filePath = b.FilePath,
                    fileName = Path.GetFileName(b.FilePath),
                    line = b.LineNumber,
                    verified = b.Verified,
                    hitCount = b.HitCount
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting session info");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_stop_all")]
    [Description("See skills/debug/session/debug_stop_all.md only when using this tool")]
    public async Task<string> StopAllAsync()
    {
        try
        {
            logger.LogInformation("Stopping all debug sessions");

            var sessions = sessionManager.GetAllSessions().ToList();
            var successCount = 0;
            var errorCount = 0;
            var errors = new List<string>();

            foreach (var session in sessions)
            {
                try
                {
                    await miClient.DisconnectAsync(session.SessionId);
                    session.State = DebugSessionState.Terminated;
                    sessionManager.RemoveSession(session.SessionId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error stopping session {SessionId}", session.SessionId);
                    errorCount++;
                    errors.Add($"{session.SessionId}: {ex.Message}");
                }
            }

            logger.LogInformation("Stopped {SuccessCount} sessions, {ErrorCount} errors", 
                successCount, errorCount);

            return JsonSerializer.Serialize(new
            {
                success = errorCount == 0,
                stoppedCount = successCount,
                errorCount,
                errors = errorCount > 0 ? errors : null,
                message = $"Stopped {successCount} session(s)"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping all sessions");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Parse command line arguments string into array.
    /// Handles quoted arguments with spaces.
    /// </summary>
    private static string[] ParseArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return [];

        var args = new List<string>();
        var currentArg = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in arguments)
        {
            switch (c)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ' ' when !inQuotes:
                {
                    if (currentArg.Length > 0)
                    {
                        args.Add(currentArg.ToString());
                        currentArg.Clear();
                    }

                    break;
                }
                default:
                    currentArg.Append(c);
                    break;
            }
        }

        if (currentArg.Length > 0)
        {
            args.Add(currentArg.ToString());
        }

        return args.ToArray();
    }
}