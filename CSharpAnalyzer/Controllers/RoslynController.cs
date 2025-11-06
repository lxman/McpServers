using CSharpAnalyzer.Core.Models.Roslyn;
using CSharpAnalyzer.Core.Services.Roslyn;
using Microsoft.AspNetCore.Mvc;

namespace CSharpAnalyzer.Controllers;

/// <summary>
/// Roslyn-based C# code analysis endpoints
/// </summary>
[ApiController]
[Route("api/roslyn")]
public class RoslynController(ILogger<RoslynController> logger) : ControllerBase
{
    /// <summary>
    /// Analyze C# code for errors, warnings, and diagnostics using Roslyn
    /// </summary>
    [HttpPost("analyze")]
    public IActionResult AnalyzeCode([FromBody] AnalyzeCodeRequest request)
    {
        try
        {
            logger.LogInformation("AnalyzeCode called with code length: {Length}", request.Code.Length);
            AnalyzeCodeResponse result = RoslynAnalysisService.AnalyzeCodeAsync(request.Code, request.FilePath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing code");
            return StatusCode(500, new { error = ex.Message, success = false });
        }
    }

    /// <summary>
    /// Get all symbols (classes, methods, properties, etc.) from C# code
    /// </summary>
    [HttpPost("symbols")]
    public async Task<IActionResult> GetSymbols([FromBody] GetSymbolsRequest request)
    {
        try
        {
            logger.LogInformation("GetSymbols called with code length: {Length}, filter: {Filter}",
                request.Code.Length, request.Filter ?? "none");
            GetSymbolsResponse result = await RoslynAnalysisService.GetSymbolsAsync(
                request.Code, request.FilePath, request.Filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting symbols");
            return StatusCode(500, new
            {
                error = ex.Message,
                symbols = Array.Empty<object>(),
                totalCount = 0
            });
        }
    }

    /// <summary>
    /// Format C# code using Roslyn formatting rules
    /// </summary>
    [HttpPost("format")]
    public async Task<IActionResult> FormatCode([FromBody] FormatCodeRequest request)
    {
        try
        {
            logger.LogInformation("FormatCode called with code length: {Length}", request.Code.Length);
            FormatCodeResponse result = await RoslynAnalysisService.FormatCodeAsync(request.Code, request.FilePath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error formatting code");
            return StatusCode(500, new
            {
                error = ex.Message,
                success = false,
                formattedCode = request.Code
            });
        }
    }

    /// <summary>
    /// Get type information at a specific position in C# code
    /// </summary>
    [HttpPost("type-info")]
    public async Task<IActionResult> GetTypeInfo([FromBody] GetTypeInfoRequest request)
    {
        try
        {
            logger.LogInformation("GetTypeInfo called at line {Line}, column {Column}",
                request.Line, request.Column);
            GetTypeInfoResponse result = await RoslynAnalysisService.GetTypeInfoAsync(
                request.Code, request.Line, request.Column, request.FilePath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting type info");
            return StatusCode(500, new { error = ex.Message, success = false });
        }
    }

    /// <summary>
    /// Calculate code metrics including cyclomatic complexity, lines of code, and more
    /// </summary>
    [HttpPost("metrics")]
    public async Task<IActionResult> CalculateMetrics([FromBody] CalculateMetricsRequest request)
    {
        try
        {
            logger.LogInformation("CalculateMetrics called with code length: {Length}", request.Code.Length);
            CalculateMetricsResponse result = await RoslynAnalysisService.CalculateMetricsAsync(
                request.Code, request.FilePath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating metrics");
            return StatusCode(500, new { error = ex.Message, success = false });
        }
    }

    /// <summary>
    /// Remove unused using directives from C# code
    /// </summary>
    [HttpPost("remove-unused-usings")]
    public async Task<IActionResult> RemoveUnusedUsings([FromBody] RemoveUnusedUsingsRequest request)
    {
        try
        {
            logger.LogInformation("RemoveUnusedUsings called with code length: {Length}", request.Code.Length);
            RemoveUnusedUsingsResponse result = await RoslynAnalysisService.RemoveUnusedUsingsAsync(
                request.Code, request.FilePath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing unused usings");
            return StatusCode(500, new
            {
                error = ex.Message,
                success = false,
                cleanedCode = request.Code
            });
        }
    }

    /// <summary>
    /// Find dead code including unreachable code and unused private members
    /// </summary>
    [HttpPost("find-dead-code")]
    public async Task<IActionResult> FindDeadCode([FromBody] FindDeadCodeRequest request)
    {
        try
        {
            logger.LogInformation("FindDeadCode called with code length: {Length}", request.Code.Length);
            FindDeadCodeResponse result = await RoslynAnalysisService.FindDeadCodeAsync(
                request.Code, request.FilePath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding dead code");
            return StatusCode(500, new { error = ex.Message, success = false });
        }
    }

    /// <summary>
    /// Get code fix suggestions for diagnostics (errors and warnings)
    /// </summary>
    [HttpPost("code-fixes")]
    public async Task<IActionResult> GetCodeFixes([FromBody] GetCodeFixesRequest request)
    {
        try
        {
            logger.LogInformation("GetCodeFixes called with code length: {Length}", request.Code.Length);
            GetCodeFixesResponse result = await RoslynAnalysisService.GetCodeFixesAsync(
                request.Code, request.FilePath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting code fixes");
            return StatusCode(500, new { error = ex.Message, success = false });
        }
    }
}

// Request models
public record AnalyzeCodeRequest(string Code, string? FilePath = null);
public record GetSymbolsRequest(string Code, string? FilePath = null, string? Filter = null);
public record FormatCodeRequest(string Code, string? FilePath = null);
public record GetTypeInfoRequest(string Code, int Line, int Column, string? FilePath = null);
public record CalculateMetricsRequest(string Code, string? FilePath = null);
public record RemoveUnusedUsingsRequest(string Code, string? FilePath = null);
public record FindDeadCodeRequest(string Code, string? FilePath = null);
public record GetCodeFixesRequest(string Code, string? FilePath = null);