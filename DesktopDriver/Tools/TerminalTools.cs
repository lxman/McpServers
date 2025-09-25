using System.ComponentModel;
using DesktopDriver.Services;
using ModelContextProtocol.Server;

namespace DesktopDriver.Tools;

[McpServerToolType]
public class TerminalTools
{
    private readonly ProcessManager _processManager;
    private readonly SecurityManager _securityManager;
    private readonly AuditLogger _auditLogger;

    public TerminalTools(ProcessManager processManager, SecurityManager securityManager, AuditLogger auditLogger)
    {
        _processManager = processManager;
        _securityManager = securityManager;
        _auditLogger = auditLogger;
    }

    [McpServerTool]
    [Description("Execute a terminal command with timeout and security checks")]
    public async Task<string> ExecuteCommand(
        [Description("Command to execute")] string command,
        [Description("Session ID for process management")] string sessionId = "default",
        [Description("Timeout in milliseconds")] int timeoutMs = 30000,
        [Description("Working directory path")] string? workingDirectory = null)
    {
        if (_securityManager.IsCommandBlocked(command))
        {
            var error = $"Command blocked by security policy: {command}";
            _auditLogger.LogCommandExecution(command, false, error);
            return error;
        }

        if (workingDirectory != null && !_securityManager.IsDirectoryAllowed(workingDirectory))
        {
            var error = $"Working directory not allowed: {workingDirectory}";
            _auditLogger.LogCommandExecution(command, false, error);
            return error;
        }

        var result = await _processManager.StartProcessAsync(command, sessionId, timeoutMs, workingDirectory);
        
        if (result.IsTimeout)
        {
            return $"Process started successfully (PID: {result.ProcessId})\n" +
                   $"Output so far:\n{result.Output}\n" +
                   $"Error output:\n{result.Error}\n" +
                   $"Process is still running. Use ReadProcessOutput to get more output.";
        }

        return $"Command executed (Exit Code: {result.ExitCode})\n" +
               $"Output:\n{result.Output}\n" +
               $"Error output:\n{result.Error}";
    }

    [McpServerTool]
    [Description("Read output from a running process")]
    public string ReadProcessOutput(
        [Description("Session ID of the process")] string sessionId = "default")
    {
        var result = _processManager.GetProcessOutput(sessionId);
        if (result == null)
        {
            return $"No process found for session: {sessionId}";
        }

        return $"Process Status: {(result.IsRunning ? "Running" : "Stopped")}\n" +
               $"PID: {result.ProcessId}\n" +
               $"Exit Code: {result.ExitCode}\n" +
               $"Output:\n{result.Output}\n" +
               $"Error output:\n{result.Error}";
    }

    [McpServerTool]
    [Description("Send input to a running interactive process")]
    public async Task<string> SendInput(
        [Description("Session ID of the process")] string sessionId,
        [Description("Input to send to the process")] string input)
    {
        var success = await _processManager.SendInputAsync(sessionId, input);
        if (success)
        {
            _auditLogger.LogOperation("Send_Input", $"Session: {sessionId}, Input: {input}", true);
            return $"Input sent successfully to session {sessionId}";
        }
        else
        {
            _auditLogger.LogOperation("Send_Input", $"Session: {sessionId}, Input: {input}", false);
            return $"Failed to send input to session {sessionId}. Process may not be running or may not accept input.";
        }
    }

    [McpServerTool]
    [Description("Kill a running process by session ID")]
    public string KillProcess(
        [Description("Session ID of the process to kill")] string sessionId)
    {
        var success = _processManager.KillProcess(sessionId);
        return success ? 
            $"Process in session {sessionId} has been terminated." : 
            $"Failed to kill process in session {sessionId}. Process may not exist or already be terminated.";
    }

    [McpServerTool]
    [Description("List all active terminal sessions")]
    public string ListSessions()
    {
        var sessions = _processManager.ListActiveSessions();
        if (!sessions.Any())
        {
            return "No active terminal sessions.";
        }

        var result = "Active Terminal Sessions:\n";
        foreach (var session in sessions)
        {
            result += $"Session: {session.SessionId}\n" +
                     $"  PID: {session.ProcessId}\n" +
                     $"  Command: {session.Command}\n" +
                     $"  Started: {session.StartTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                     $"  Status: {(session.IsRunning ? "Running" : $"Stopped (Exit: {session.ExitCode})")}\n\n";
        }

        return result;
    }
}
