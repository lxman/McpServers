using System.ComponentModel;
using System.Text.Json;
using CSharpAnalyzerMcp.Models;
using CSharpAnalyzerMcp.Models.Roslyn;
using CSharpAnalyzerMcp.Services.Roslyn;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools.Roslyn;

/// <summary>
/// Provides Roslyn-based C# code analysis tools through MCP.
/// </summary>
[McpServerToolType]
public class RoslynTools(
    RoslynAnalysisService analysisService,
    ILogger<RoslynTools> logger)
{
    /// <summary>
    /// Analyze C# code for errors, warnings, and diagnostics using Roslyn.
    /// </summary>
    [McpServerTool]
    [Description("Analyze C# code for errors, warnings, and diagnostics using Roslyn")]
    public async Task<string> AnalyzeCode(
        [Description("C# code to analyze")] string code,
        [Description("Optional file path for context")] string? filePath = null)
    {
        logger.LogInformation("AnalyzeCode called with code length: {Length}", code.Length);

        try
        {
            AnalyzeCodeResponse result = await RoslynAnalysisService.AnalyzeCodeAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing code");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                success = false
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Get all symbols (classes, methods, properties, etc.) from C# code.
    /// </summary>
    [McpServerTool]
    [Description("Get all symbols (classes, methods, properties, etc.) from C# code")]
    public async Task<string> GetSymbols(
        [Description("C# code to analyze")] string code,
        [Description("Optional file path for context")] string? filePath = null,
        [Description("Optional filter: 'class', 'method', 'property', etc.")] string? filter = null)
    {
        logger.LogInformation("GetSymbols called with code length: {Length}, filter: {Filter}", 
            code.Length, filter ?? "none");

        try
        {
            GetSymbolsResponse result = await analysisService.GetSymbolsAsync(code, filePath, filter);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting symbols");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                symbols = Array.Empty<object>(),
                totalCount = 0
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Format C# code using Roslyn formatting rules.
    /// </summary>
    [McpServerTool]
    [Description("Format C# code using Roslyn formatting rules")]
    public async Task<string> FormatCode(
        [Description("C# code to format")] string code,
        [Description("Optional file path for context")] string? filePath = null)
    {
        logger.LogInformation("FormatCode called with code length: {Length}", code.Length);

        try
        {
            FormatCodeResponse result = await RoslynAnalysisService.FormatCodeAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error formatting code");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                success = false,
                formattedCode = code
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Get type information at a specific position in C# code.
    /// </summary>
    [McpServerTool]
    [Description("Get type information at a specific position in C# code")]
    public async Task<string> GetTypeInfo(
        [Description("C# code to analyze")] string code,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Optional file path for context")] string? filePath = null)
    {
        logger.LogInformation("GetTypeInfo called at line {Line}, column {Column}", line, column);

        try
        {
            GetTypeInfoResponse result = await RoslynAnalysisService.GetTypeInfoAsync(code, line, column, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting type info");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                success = false
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// Calculate code metrics including cyclomatic complexity.
    /// </summary>
    [McpServerTool]
    [Description("Calculate code metrics including cyclomatic complexity, lines of code, and more")]
    public async Task<string> CalculateMetrics(
        [Description("C# code to analyze")] string code,
        [Description("Optional file path for context")] string? filePath = null)
    {
        logger.LogInformation("CalculateMetrics called with code length: {Length}", code.Length);

        try
        {
            CalculateMetricsResponse result = await RoslynAnalysisService.CalculateMetricsAsync(code, filePath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating metrics");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                success = false
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
}