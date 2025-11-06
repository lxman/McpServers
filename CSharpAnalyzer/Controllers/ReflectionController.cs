using CSharpAnalyzer.Core.Models.Reflection;
using CSharpAnalyzer.Core.Services.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace CSharpAnalyzer.Controllers;

/// <summary>
/// .NET assembly reflection and analysis endpoints
/// </summary>
[ApiController]
[Route("api/reflection")]
public class ReflectionController(AssemblyAnalysisService analysisService, ILogger<ReflectionController> logger)
    : ControllerBase
{
    /// <summary>
    /// Get detailed metadata and information about a .NET assembly including version, references, and target framework
    /// </summary>
    [HttpGet("assembly-info")]
    public IActionResult GetAssemblyInfo([FromQuery] string assemblyPath)
    {
        try
        {
            logger.LogInformation("GetAssemblyInfo called for: {AssemblyPath}", assemblyPath);
            AssemblyInfoResponse result = analysisService.GetAssemblyInfo(assemblyPath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting assembly info");
            return StatusCode(500, new { error = ex.Message, success = false });
        }
    }

    /// <summary>
    /// List all types (classes, interfaces, enums, structs) in a .NET assembly with optional filtering
    /// </summary>
    [HttpPost("list-types")]
    public IActionResult ListTypes([FromBody] ListTypesRequest request)
    {
        try
        {
            logger.LogInformation(
                "ListTypes called for: {AssemblyPath}, publicOnly: {PublicOnly}, namespace: {Namespace}, kind: {Kind}",
                request.AssemblyPath, request.PublicOnly, request.NamespaceFilter ?? "all", request.TypeKindFilter ?? "all");
            
            ListTypesResponse result = analysisService.ListTypes(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing types");
            return StatusCode(500, new
            {
                error = ex.Message,
                success = false,
                types = Array.Empty<object>(),
                totalCount = 0
            });
        }
    }
}