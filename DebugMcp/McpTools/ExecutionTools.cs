using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using DebugMcp.Common;
using DebugMcp.Models;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.McpTools;

/// <summary>
/// MCP tools for controlling program execution
/// </summary>
[McpServerToolType]
public class ExecutionTools(
    DebuggerSessionManager sessionManager, 
    MiClient miClient, 
    ILogger<ExecutionTools> logger)
{
    [McpServerTool, DisplayName("debug_run")]
    [Description("Begin execution of the debugged program")]
    public async Task<string> RunAsync(
        [Description("Session ID from debug_launch")] string sessionId)
    {
        try
        {
            DebugSession? session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Session not found: {sessionId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Running program for session {SessionId}", sessionId);

            // Send MI command: -exec-run
            // This will return: ^running, then eventually *stopped
            var command = "-exec-run";
            MiResponse? response = await miClient.SendCommandAsync(
                sessionId, 
                command,
                TimeSpan.FromSeconds(60)); // Longer timeout for startup

            if (response is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No response from debugger (timeout or connection closed)"
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse the stop reason from *stopped record
            StoppedInfo stoppedInfo = ParseStoppedInfo(response);

            if (response.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Program started and stopped",
                    stopReason = stoppedInfo.Reason,
                    threadId = stoppedInfo.ThreadId,
                    frame = stoppedInfo.Frame,
                    allRecords = response.Records
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Extract error message
            string errorMsg = ExtractErrorMessage(response);
            logger.LogError("Run command failed: {Error}", errorMsg);

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = errorMsg,
                resultClass = response.ResultClass,
                allRecords = response.Records
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running program");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                stackTrace = ex.StackTrace
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_continue")]
    [Description("Continue execution until next breakpoint or program termination")]
    public async Task<string> ContinueAsync(
        [Description("Session ID from debug_launch")] string sessionId)
    {
        try
        {
            DebugSession? session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Session not found: {sessionId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Continuing execution for session {SessionId}", sessionId);

            // Send MI command: -exec-continue
            // This will return: ^running, then eventually *stopped or exit
            const string command = "-exec-continue";
            MiResponse? response = await miClient.SendCommandAsync(
                sessionId, 
                command,
                TimeSpan.FromSeconds(60)); // Longer timeout for execution

            if (response is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No response from debugger (timeout or connection closed)"
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse the stop reason from *stopped record
            StoppedInfo stoppedInfo = ParseStoppedInfo(response);

            if (response.Success)
            {
                // Check if program exited
                if (stoppedInfo.Reason == "exited")
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        message = "Program exited",
                        exitCode = stoppedInfo.ExitCode,
                        allRecords = response.Records
                    }, SerializerOptions.JsonOptionsIndented);
                }

                // Stopped at breakpoint or step
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Execution stopped",
                    stopReason = stoppedInfo.Reason,
                    threadId = stoppedInfo.ThreadId,
                    breakpointNumber = stoppedInfo.BreakpointNumber,
                    frame = stoppedInfo.Frame,
                    allRecords = response.Records
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Extract error message
            string errorMsg = ExtractErrorMessage(response);
            logger.LogError("Continue command failed: {Error}", errorMsg);

            return JsonSerializer.Serialize(new
            {
                success = false,
                error = errorMsg,
                resultClass = response.ResultClass,
                allRecords = response.Records
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error continuing execution");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_step_over")]
    [Description("Step over one source line (does not enter functions)")]
    public async Task<string> StepOverAsync(
        [Description("Session ID from debug_launch")] string sessionId)
    {
        try
        {
            DebugSession? session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Session not found: {sessionId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Stepping over for session {SessionId}", sessionId);

            // Send MI command: -exec-next
            const string command = "-exec-next";
            MiResponse? response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No response from debugger"
                }, SerializerOptions.JsonOptionsIndented);
            }

            StoppedInfo stoppedInfo = ParseStoppedInfo(response);

            if (response.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Stepped over",
                    stopReason = stoppedInfo.Reason,
                    frame = stoppedInfo.Frame
                }, SerializerOptions.JsonOptionsIndented);
            }

            string errorMsg = ExtractErrorMessage(response);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = errorMsg
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stepping over");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_step_into")]
    [Description("Step into one source line (enters functions)")]
    public async Task<string> StepIntoAsync(
        [Description("Session ID from debug_launch")] string sessionId)
    {
        try
        {
            DebugSession? session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Session not found: {sessionId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Stepping into for session {SessionId}", sessionId);

            // Send MI command: -exec-step
            const string command = "-exec-step";
            MiResponse? response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No response from debugger"
                }, SerializerOptions.JsonOptionsIndented);
            }

            StoppedInfo stoppedInfo = ParseStoppedInfo(response);

            if (response.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Stepped into",
                    stopReason = stoppedInfo.Reason,
                    frame = stoppedInfo.Frame
                }, SerializerOptions.JsonOptionsIndented);
            }

            string errorMsg = ExtractErrorMessage(response);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = errorMsg
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stepping into");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_step_out")]
    [Description("Step out of current function")]
    public async Task<string> StepOutAsync(
        [Description("Session ID from debug_launch")] string sessionId)
    {
        try
        {
            DebugSession? session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Session not found: {sessionId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Stepping out for session {SessionId}", sessionId);

            // Send MI command: -exec-finish
            const string command = "-exec-finish";
            MiResponse? response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No response from debugger"
                }, SerializerOptions.JsonOptionsIndented);
            }

            StoppedInfo stoppedInfo = ParseStoppedInfo(response);

            if (response.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Stepped out",
                    stopReason = stoppedInfo.Reason,
                    frame = stoppedInfo.Frame
                }, SerializerOptions.JsonOptionsIndented);
            }

            string errorMsg = ExtractErrorMessage(response);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = errorMsg
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stepping out");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Parse stop information from *stopped record in response.
    /// </summary>
    private static StoppedInfo ParseStoppedInfo(MiResponse response)
    {
        var info = new StoppedInfo();

        // Find the *stopped record
        string? stoppedRecord = response.GetAsyncExecRecords()
            .FirstOrDefault(r => r.StartsWith("*stopped"));

        if (stoppedRecord is null)
        {
            // Check if program exited
            stoppedRecord = response.GetAsyncExecRecords()
                .FirstOrDefault(r => r.StartsWith("*"));
            
            if (stoppedRecord is null)
                return info;
        }

        // Parse reason
        Match reasonMatch = Regex.Match(stoppedRecord, @"reason=""([^""]+)""");
        if (reasonMatch.Success)
        {
            info.Reason = reasonMatch.Groups[1].Value;
        }

        // Parse thread-id
        Match threadMatch = Regex.Match(stoppedRecord, @"thread-id=""([^""]+)""");
        if (threadMatch.Success)
        {
            info.ThreadId = threadMatch.Groups[1].Value;
        }

        // Parse breakpoint number
        Match bkptMatch = Regex.Match(stoppedRecord, @"bkptno=""([^""]+)""");
        if (bkptMatch.Success && int.TryParse(bkptMatch.Groups[1].Value, out int bkptNo))
        {
            info.BreakpointNumber = bkptNo;
        }

        // Parse exit code
        Match exitMatch = Regex.Match(stoppedRecord, @"exit-code=""([^""]+)""");
        if (exitMatch.Success)
        {
            info.ExitCode = exitMatch.Groups[1].Value;
        }

        // Parse frame info (basic extraction)
        Match frameMatch = Regex.Match(stoppedRecord, @"frame=\{([^}]+)\}");
        if (!frameMatch.Success) return info;
        string frameData = frameMatch.Groups[1].Value;
            
        // Extract file
        Match fileMatch = Regex.Match(frameData, @"file=""([^""]+)""");
        if (fileMatch.Success)
        {
            info.Frame ??= new FrameInfo();
            info.Frame.File = fileMatch.Groups[1].Value;
        }

        // Extract line
        Match lineMatch = Regex.Match(frameData, @"line=""(\d+)""");
        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out int line))
        {
            info.Frame ??= new FrameInfo();
            info.Frame.Line = line;
        }

        // Extract function
        Match funcMatch = Regex.Match(frameData, @"func=""([^""]+)""");
        if (!funcMatch.Success) return info;
        info.Frame ??= new FrameInfo();
        info.Frame.Function = funcMatch.Groups[1].Value;

        return info;
    }

    /// <summary>
    /// Extract error message from MI response.
    /// </summary>
    private static string ExtractErrorMessage(MiResponse response)
    {
        string? resultRecord = response.GetResultRecord();
        if (string.IsNullOrEmpty(resultRecord))
            return "Empty response from debugger";

        // Look for error message: ^error,msg="..."
        Match match = Regex.Match(resultRecord, @"msg=""([^""]+)""");
        return match.Success
            ? match.Groups[1].Value
            : $"Command failed: {response.ResultClass}";
    }
}