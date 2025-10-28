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
/// MCP tools for inspecting program state
/// </summary>
[McpServerToolType]
public class InspectionTools(
    DebuggerSessionManager sessionManager,
    MiClient miClient,
    ILogger<InspectionTools> logger)
{
    [McpServerTool, DisplayName("debug_get_stack_trace")]
    [Description("Get the call stack when execution is paused at a breakpoint")]
    public async Task<string> GetStackTraceAsync(
        [Description("Session ID from debug_launch")] string sessionId,
        [Description("Thread ID (use 1 for main thread)")] int threadId = 1)
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

            logger.LogInformation("Getting stack trace for session {SessionId}, thread {ThreadId}", 
                sessionId, threadId);

            // Send MI command: -stack-list-frames
            const string command = "-stack-list-frames";
            MiResponse? response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null || !response.Success)
            {
                string errorMsg = ExtractErrorMessage(response);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = errorMsg
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse stack frames from response
            // Expected format: ^done,stack=[frame={level="0",...},frame={level="1",...}]
            List<StackFrameInfo> frames = ParseStackFrames(response);

            return JsonSerializer.Serialize(new
            {
                success = true,
                frameCount = frames.Count,
                frames = frames.Select(f => new
                {
                    level = f.Level,
                    function = f.Function,
                    file = f.File,
                    fileName = f.File != null ? Path.GetFileName(f.File) : null,
                    line = f.Line,
                    address = f.Address
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting stack trace");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_get_variables")]
    [Description("Get local variables when execution is paused")]
    public async Task<string> GetVariablesAsync(
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

            logger.LogInformation("Getting variables for session {SessionId}", sessionId);

            // Send MI command: -stack-list-variables
            // Use --simple-values to get primitive values inline
            const string command = "-stack-list-variables --simple-values";
            MiResponse? response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null || !response.Success)
            {
                string errorMsg = ExtractErrorMessage(response);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = errorMsg
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse variables from response
            // Expected format: ^done,variables=[{name="x",value="42"},{name="y",value="\"hello\""}]
            List<VariableInfo> variables = ParseVariables(response);

            return JsonSerializer.Serialize(new
            {
                success = true,
                variableCount = variables.Count,
                variables = variables.Select(v => new
                {
                    name = v.Name,
                    value = v.Value,
                    type = v.Type
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting variables");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_evaluate_expression")]
    [Description("Evaluate an expression in the current context")]
    public async Task<string> EvaluateExpressionAsync(
        [Description("Session ID from debug_launch")] string sessionId,
        [Description("Expression to evaluate (e.g., 'x + y', 'myObject.Property')")] string expression)
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

            logger.LogInformation("Evaluating expression '{Expression}' for session {SessionId}", 
                expression, sessionId);

            // Send MI command: -data-evaluate-expression <expr>
            var command = $"-data-evaluate-expression \"{expression}\"";
            MiResponse? response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null || !response.Success)
            {
                string errorMsg = ExtractErrorMessage(response);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = errorMsg,
                    expression
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse result from response
            // Expected format: ^done,value="..."
            string? resultValue = ExtractValue(response);

            return JsonSerializer.Serialize(new
            {
                success = true,
                expression,
                value = resultValue
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error evaluating expression");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                expression
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("debug_get_threads")]
    [Description("Get all threads in the debugged process")]
    public async Task<string> GetThreadsAsync(
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

            logger.LogInformation("Getting threads for session {SessionId}", sessionId);

            // Send MI command: -thread-info
            const string command = "-thread-info";
            MiResponse? response = await miClient.SendCommandAsync(sessionId, command);

            if (response is null || !response.Success)
            {
                string errorMsg = ExtractErrorMessage(response);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = errorMsg
                }, SerializerOptions.JsonOptionsIndented);
            }

            // Parse threads from response
            List<ThreadInfo> threads = ParseThreads(response);

            return JsonSerializer.Serialize(new
            {
                success = true,
                threadCount = threads.Count,
                threads = threads.Select(t => new
                {
                    id = t.Id,
                    state = t.State,
                    name = t.Name,
                    frame = t.Frame != null ? new
                    {
                        function = t.Frame.Function,
                        file = t.Frame.File,
                        line = t.Frame.Line
                    } : null
                }).ToList()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting threads");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Parse stack frames from MI response.
    /// Expected format: stack=[frame={level="0",func="Main()",...}]
    /// </summary>
    private static List<StackFrameInfo> ParseStackFrames(MiResponse response)
    {
        var frames = new List<StackFrameInfo>();
        string? resultRecord = response.GetResultRecord();
        
        if (string.IsNullOrEmpty(resultRecord))
            return frames;

        // Find all frame={...} patterns
        MatchCollection frameMatches = Regex.Matches(resultRecord, @"frame=\{([^}]+)\}");
        
        foreach (Match frameMatch in frameMatches)
        {
            string frameData = frameMatch.Groups[1].Value;
            var frame = new StackFrameInfo();

            // Parse level
            Match levelMatch = Regex.Match(frameData, @"level=""(\d+)""");
            if (levelMatch.Success && int.TryParse(levelMatch.Groups[1].Value, out int level))
            {
                frame.Level = level;
            }

            // Parse function
            Match funcMatch = Regex.Match(frameData, @"func=""([^""]+)""");
            if (funcMatch.Success)
            {
                frame.Function = funcMatch.Groups[1].Value;
            }

            // Parse file (prefer fullname over file)
            Match fullnameMatch = Regex.Match(frameData, @"fullname=""([^""]+)""");
            if (fullnameMatch.Success)
            {
                frame.File = fullnameMatch.Groups[1].Value;
            }
            else
            {
                Match fileMatch = Regex.Match(frameData, @"file=""([^""]+)""");
                if (fileMatch.Success)
                {
                    frame.File = fileMatch.Groups[1].Value;
                }
            }

            // Parse line
            Match lineMatch = Regex.Match(frameData, @"line=""(\d+)""");
            if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out int line))
            {
                frame.Line = line;
            }

            // Parse address
            Match addrMatch = Regex.Match(frameData, @"addr=""(0x[0-9a-fA-F]+)""");
            if (addrMatch.Success)
            {
                frame.Address = addrMatch.Groups[1].Value;
            }

            frames.Add(frame);
        }

        return frames;
    }

    /// <summary>
    /// Parse variables from MI response.
    /// Expected format: variables=[{name="x",value="42"},{name="y",value="\"str\""}]
    /// </summary>
    private static List<VariableInfo> ParseVariables(MiResponse response)
    {
        var variables = new List<VariableInfo>();
        string? resultRecord = response.GetResultRecord();
        
        if (string.IsNullOrEmpty(resultRecord))
            return variables;

        // Find all {name="...",value="..."} patterns
        MatchCollection varMatches = Regex.Matches(resultRecord, @"\{name=""([^""]+)"",value=""([^""]*)""\}");
        
        foreach (Match varMatch in varMatches)
        {
            var variable = new VariableInfo
            {
                Name = varMatch.Groups[1].Value,
                Value = varMatch.Groups[2].Value
            };

            // Try to infer type from value
            variable.Type = InferType(variable.Value);

            variables.Add(variable);
        }

        return variables;
    }

    /// <summary>
    /// Parse threads from MI response.
    /// Expected format: threads=[{id="1",state="stopped",...}]
    /// </summary>
    private static List<ThreadInfo> ParseThreads(MiResponse response)
    {
        var threads = new List<ThreadInfo>();
        string? resultRecord = response.GetResultRecord();
        
        if (string.IsNullOrEmpty(resultRecord))
            return threads;

        // Find all thread entries
        MatchCollection threadMatches = Regex.Matches(resultRecord, @"\{id=""([^""]+)""[^}]*\}");
        
        foreach (Match threadMatch in threadMatches)
        {
            string threadData = threadMatch.Value;
            var thread = new ThreadInfo();

            // Parse id
            Match idMatch = Regex.Match(threadData, @"id=""([^""]+)""");
            if (idMatch.Success)
            {
                thread.Id = idMatch.Groups[1].Value;
            }

            // Parse state
            Match stateMatch = Regex.Match(threadData, @"state=""([^""]+)""");
            if (stateMatch.Success)
            {
                thread.State = stateMatch.Groups[1].Value;
            }

            // Parse name
            Match nameMatch = Regex.Match(threadData, @"name=""([^""]+)""");
            if (nameMatch.Success)
            {
                thread.Name = nameMatch.Groups[1].Value;
            }

            // Parse frame if present
            Match frameMatch = Regex.Match(threadData, @"frame=\{([^}]+)\}");
            if (frameMatch.Success)
            {
                string frameData = frameMatch.Groups[1].Value;
                thread.Frame = new FrameInfo();

                Match funcMatch = Regex.Match(frameData, @"func=""([^""]+)""");
                if (funcMatch.Success)
                {
                    thread.Frame.Function = funcMatch.Groups[1].Value;
                }

                Match fileMatch = Regex.Match(frameData, @"file=""([^""]+)""");
                if (fileMatch.Success)
                {
                    thread.Frame.File = fileMatch.Groups[1].Value;
                }

                Match lineMatch = Regex.Match(frameData, @"line=""(\d+)""");
                if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out int line))
                {
                    thread.Frame.Line = line;
                }
            }

            threads.Add(thread);
        }

        return threads;
    }

    /// <summary>
    /// Extract value from -data-evaluate-expression response.
    /// Expected format: ^done,value="..."
    /// </summary>
    private static string? ExtractValue(MiResponse response)
    {
        string? resultRecord = response.GetResultRecord();
        if (string.IsNullOrEmpty(resultRecord))
            return null;

        Match match = Regex.Match(resultRecord, @"value=""([^""]*)""");
        return match.Success
            ? match.Groups[1].Value
            : null;
    }

    /// <summary>
    /// Infer variable type from its value representation.
    /// </summary>
    private static string InferType(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "unknown";

        switch (value)
        {
            // null
            case "null":
                return "null";
            // Boolean
            case "true":
            case "false":
                return "bool";
        }

        // String (quoted)
        if (value.StartsWith("\"") && value.EndsWith("\""))
            return "string";

        // Number (integer or float)
        if (int.TryParse(value, out _))
            return "int";
        if (double.TryParse(value, out _))
            return "number";

        // Object/Array (starts with { or [)
        if (value.StartsWith("{"))
            return "object";
        return value.StartsWith("[") ? "array" : "unknown";
    }

    /// <summary>
    /// Extract error message from MI response.
    /// </summary>
    private static string ExtractErrorMessage(MiResponse? response)
    {
        if (response is null)
            return "No response received from debugger";

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