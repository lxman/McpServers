using Microsoft.AspNetCore.Mvc;
using DesktopCommander.Services;

namespace DesktopCommander.Controllers;

/// <summary>
/// Terminal and command execution operations API
/// </summary>
[ApiController]
[Route("api/terminal")]
public class TerminalController(
    ProcessManager processManager,
    SecurityManager securityManager,
    ILogger<TerminalController> logger) : ControllerBase
{
    /// <summary>
    /// Execute a terminal command
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] ExecuteCommandRequest request)
    {
        try
        {
            if (securityManager.IsCommandBlocked(request.Command))
            {
                return StatusCode(403, new { success = false, error = "Command is blocked" });
            }

            ProcessResult result = await processManager.StartProcessAsync(
                request.Command,
                request.SessionId,
                request.TimeoutMs,
                request.WorkingDirectory);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Send input to a running interactive process
    /// </summary>
    [HttpPost("sessions/{sessionId}/input")]
    public async Task<IActionResult> SendInput(string sessionId, [FromBody] SendInputRequest request)
    {
        try
        {
            bool result = await processManager.SendInputAsync(sessionId, request.Input);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending input to session: {SessionId}", sessionId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Read output from a running process
    /// </summary>
    [HttpGet("sessions/{sessionId}/output")]
    public IActionResult ReadOutput(string sessionId = "default")
    {
        try
        {
            ProcessResult? result = processManager.GetProcessOutput(sessionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading output from session: {SessionId}", sessionId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// List all active terminal sessions
    /// </summary>
    [HttpGet("sessions")]
    public IActionResult ListSessions()
    {
        try
        {
            List<ProcessInfo> result = processManager.ListActiveSessions();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing sessions");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Kill a running process by session ID
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public IActionResult KillSession(string sessionId)
    {
        try
        {
            bool result = processManager.KillProcess(sessionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error killing session: {SessionId}", sessionId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

// Request models
public record ExecuteCommandRequest(
    string Command,
    string? WorkingDirectory = null,
    int TimeoutMs = 30000,
    string SessionId = "default");

public record SendInputRequest(string Input);