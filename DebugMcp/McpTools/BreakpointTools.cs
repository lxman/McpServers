using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using DebugServer.Core.Common;
using DebugServer.Core.Models;
using DebugServer.Core.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.McpTools;

/// <summary>
/// MCP tools for managing breakpoints
/// </summary>
[McpServerToolType]
public partial class BreakpointTools(
    DebuggerSessionManager sessionManager,
    MiClient miClient,
    ILogger<BreakpointTools> logger)
{
    [McpServerTool, DisplayName("debug_set_breakpoint")]
    [Description("Set a breakpoint at a specific file and line number")]
    public async Task<string> SetBreakpointAsync(
        string sessionId,
        string filePath,
        int lineNumber)
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

            logger.LogInformation("Setting breakpoint at {File}:{Line} for session {SessionId}", 
                filePath, lineNumber, sessionId);

            // Send MI command: -break-insert <file>:<line>
            var command = $"-break-insert {filePath}:{lineNumber}";
            var response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null || !response.Success)
            {
                var errorMsg = ExtractErrorMessage(response);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = errorMsg,
                    miRecords = response?.Records
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse the breakpoint number from MI response
            // Expected format: 123^done,bkpt={number="1",...}
            var breakpointNumber = ParseBreakpointNumber(response);
            if (breakpointNumber == 0)
            {
                logger.LogWarning("Could not parse breakpoint number from response");
                breakpointNumber = session.Breakpoints.Count + 1; // Fallback
            }

            // Extract additional breakpoint info
            var breakpointInfo = ParseBreakpointInfo(response);

            var breakpoint = new Breakpoint
            {
                Id = breakpointNumber,
                FilePath = breakpointInfo.FilePath ?? filePath,
                LineNumber = breakpointInfo.LineNumber ?? lineNumber,
                Verified = breakpointInfo.Verified,
                HitCount = 0
            };

            session.Breakpoints.Add(breakpoint);

            return JsonSerializer.Serialize(new
            {
                success = true,
                breakpointId = breakpoint.Id,
                filePath = breakpoint.FilePath,
                line = breakpoint.LineNumber,
                verified = breakpoint.Verified,
                warning = breakpointInfo.Warning,
                message = $"Breakpoint {breakpoint.Id} set at {Path.GetFileName(filePath)}:{lineNumber}"
            }, SerializerOptions.JsonOptionsIndented);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting breakpoint");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_list_breakpoints")]
    [Description("List all breakpoints for a debug session")]
    public string ListBreakpoints(
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

            var breakpoints = session.Breakpoints.Select(b => new
            {
                id = b.Id,
                filePath = b.FilePath,
                fileName = Path.GetFileName(b.FilePath),
                line = b.LineNumber,
                verified = b.Verified,
                hitCount = b.HitCount
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = breakpoints.Count,
                breakpoints
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing breakpoints");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_delete_breakpoint")]
    [Description("Remove a breakpoint by ID")]
    public async Task<string> DeleteBreakpointAsync(
        string sessionId,
        int breakpointId)
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

            var breakpoint = session.Breakpoints.FirstOrDefault(b => b.Id == breakpointId);
            if (breakpoint is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Breakpoint not found: {breakpointId}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogInformation("Deleting breakpoint {BreakpointId} for session {SessionId}", 
                breakpointId, sessionId);

            // Send MI command: -break-delete <number>
            var command = $"-break-delete {breakpointId}";
            var response = await miClient.SendCommandAsync(sessionId, command);
            
            if (response is null || !response.Success)
            {
                var errorMsg = ExtractErrorMessage(response);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = errorMsg,
                    miRecords = response?.Records
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Remove from session tracking
            session.Breakpoints.Remove(breakpoint);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Breakpoint {breakpointId} deleted"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting breakpoint");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Parse breakpoint number from MI response.
    /// Expected format in the result record: bkpt={number="1",...}
    /// </summary>
    private static int ParseBreakpointNumber(MiResponse response)
    {
        var resultRecord = response.GetResultRecord();
        if (string.IsNullOrEmpty(resultRecord))
            return 0;

        // Look for the number="N" pattern
        var match = Regex.Match(resultRecord, @"number=""(\d+)""");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var number))
        {
            return number;
        }

        return 0;
    }

    /// <summary>
    /// Parse detailed breakpoint information from MI response.
    /// </summary>
    private static BreakpointInfo ParseBreakpointInfo(MiResponse response)
    {
        var resultRecord = response.GetResultRecord();
        if (string.IsNullOrEmpty(resultRecord))
            return new BreakpointInfo();

        var info = new BreakpointInfo
        {
            Verified = true // Assume it is verified unless we find a warning
        };

        // Check for a warning field (unresolved breakpoint)
        var warningMatch = Regex.Match(resultRecord, @"warning=""([^""]+)""");
        if (warningMatch.Success)
        {
            info.Warning = warningMatch.Groups[1].Value;
            info.Verified = false;
        }

        // Extract file path if present
        var fileMatch = Regex.Match(resultRecord, @"file=""([^""]+)""");
        if (fileMatch.Success)
        {
            info.FilePath = fileMatch.Groups[1].Value;
        }

        // Extract fullname if present (full path)
        var fullnameMatch = Regex.Match(resultRecord, @"fullname=""([^""]+)""");
        if (fullnameMatch.Success)
        {
            info.FilePath = fullnameMatch.Groups[1].Value;
        }

        // Extract line number if present
        var lineMatch = Regex.Match(resultRecord, @"line=""(\d+)""");
        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var line))
        {
            info.LineNumber = line;
        }

        return info;
    }

    /// <summary>
    /// Extract error message from MI response.
    /// </summary>
    private static string ExtractErrorMessage(MiResponse? response)
    {
        if (response is null)
            return "No response received from debugger";

        var resultRecord = response.GetResultRecord();
        if (string.IsNullOrEmpty(resultRecord))
            return "Empty response from debugger";

        // Look for error message: ^error,msg="..."
        var match = Regex.Match(resultRecord, @"msg=""([^""]+)""");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return $"Command failed: {response.ResultClass}";
    }
}