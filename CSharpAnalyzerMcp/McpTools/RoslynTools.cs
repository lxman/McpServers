using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using CSharpAnalyzer.Core.Models.Roslyn;
using CSharpAnalyzer.Core.Services.Roslyn;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.McpTools;

/// <summary>
/// MCP tools for Roslyn-based C# code analysis
/// </summary>
[McpServerToolType]
public class RoslynTools(ILogger<RoslynTools> logger)
{
    [McpServerTool, DisplayName("analyze_code")]
    [Description("Analyze C# code for errors, warnings, and diagnostics. See skills/csharp/analyze-code.md only when using this tool")]
    public string AnalyzeCode(string code, string? filePath = null)
    {
        try
        {
            logger.LogDebug("Analyzing code with length: {Length}", code.Length);
            AnalyzeCodeResponse result = RoslynAnalysisService.AnalyzeCodeAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing code");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_symbols")]
    [Description("Get all symbols (classes, methods, properties, etc.) from C# code. See skills/csharp/get-symbols.md only when using this tool")]
    public async Task<string> GetSymbols(string code, string? filePath = null, string? filter = null)
    {
        try
        {
            logger.LogDebug("Getting symbols with filter: {Filter}", filter ?? "none");
            GetSymbolsResponse result = await RoslynAnalysisService.GetSymbolsAsync(code, filePath, filter);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting symbols");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                symbols = Array.Empty<object>(),
                totalCount = 0
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("format_code")]
    [Description("Format C# code using Roslyn formatting rules. See skills/csharp/format-code.md only when using this tool")]
    public async Task<string> FormatCode(string code, string? filePath = null)
    {
        try
        {
            logger.LogDebug("Formatting code with length: {Length}", code.Length);
            FormatCodeResponse result = await RoslynAnalysisService.FormatCodeAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error formatting code");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                formattedCode = code
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_type_info")]
    [Description("Get type information at a specific position in C# code. See skills/csharp/get-type-info.md only when using this tool")]
    public async Task<string> GetTypeInfo(string code, int line, int column, string? filePath = null)
    {
        try
        {
            logger.LogDebug("Getting type info at line {Line}, column {Column}", line, column);
            GetTypeInfoResponse result = await RoslynAnalysisService.GetTypeInfoAsync(code, line, column, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting type info");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("calculate_metrics")]
    [Description("Calculate code metrics including cyclomatic complexity and lines of code. See skills/csharp/calculate-metrics.md only when using this tool")]
    public async Task<string> CalculateMetrics(string code, string? filePath = null)
    {
        try
        {
            logger.LogDebug("Calculating metrics for code with length: {Length}", code.Length);
            CalculateMetricsResponse result = await RoslynAnalysisService.CalculateMetricsAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating metrics");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("remove_unused_usings")]
    [Description("Remove unused using directives from C# code. See skills/csharp/remove-unused-usings.md only when using this tool")]
    public async Task<string> RemoveUnusedUsings(string code, string? filePath = null)
    {
        try
        {
            logger.LogDebug("Removing unused usings from code with length: {Length}", code.Length);
            RemoveUnusedUsingsResponse result = await RoslynAnalysisService.RemoveUnusedUsingsAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing unused usings");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                cleanedCode = code
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("find_dead_code")]
    [Description("Find dead code including unreachable code and unused private members. See skills/csharp/find-dead-code.md only when using this tool")]
    public async Task<string> FindDeadCode(string code, string? filePath = null)
    {
        try
        {
            logger.LogDebug("Finding dead code in code with length: {Length}", code.Length);
            FindDeadCodeResponse result = await RoslynAnalysisService.FindDeadCodeAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding dead code");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_code_fixes")]
    [Description("Get code fix suggestions for diagnostics (errors and warnings). See skills/csharp/get-code-fixes.md only when using this tool")]
    public async Task<string> GetCodeFixes(string code, string? filePath = null)
    {
        try
        {
            logger.LogDebug("Getting code fixes for code with length: {Length}", code.Length);
            GetCodeFixesResponse result = await RoslynAnalysisService.GetCodeFixesAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting code fixes");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }
}