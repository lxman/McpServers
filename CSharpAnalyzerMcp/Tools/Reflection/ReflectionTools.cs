using System.ComponentModel;
using System.Text.Json;
using CSharpAnalyzerMcp.Models;
using CSharpAnalyzerMcp.Models.Reflection;
using CSharpAnalyzerMcp.Services.Reflection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.Tools.Reflection;

/// <summary>
/// Provides reflection-based assembly analysis tools through MCP.
/// </summary>
[McpServerToolType]
public class ReflectionTools(AssemblyAnalysisService analysisService, ILogger<ReflectionTools> logger)
{
    /// <summary>
    /// Get detailed metadata and information about a .NET assembly.
    /// </summary>
    [McpServerTool, DisplayName("get_assembly_info")]
    [Description("Get detailed metadata and information about a .NET assembly including version, references, and target framework")]
    public async Task<string> GetAssemblyInfo(
        [Description("Path to the assembly (.dll or .exe) - must be canonical")] string assemblyPath)
    {
        logger.LogInformation("GetAssemblyInfo called for: {AssemblyPath}", assemblyPath);

        try
        {
            AssemblyInfoResponse result = analysisService.GetAssemblyInfo(assemblyPath);
            return await Task.FromResult(JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting assembly info");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                success = false
            }, SerializerOptions.JsonOptionsIndented);
        }
    }

    /// <summary>
    /// List all types (classes, interfaces, enums, structs) in a .NET assembly.
    /// </summary>
    [McpServerTool, DisplayName("list_types")]
    [Description("List all types (classes, interfaces, enums, structs) in a .NET assembly with optional filtering")]
    public async Task<string> ListTypes(
        [Description("Path to the assembly (.dll or .exe) - must be canonical")] string assemblyPath,
        [Description("Only include public types (default: true)")] bool publicOnly = true,
        [Description("Filter by namespace (optional, e.g., 'System.Collections')")] string? namespaceFilter = null,
        [Description("Filter by type kind (optional: 'class', 'interface', 'enum', 'struct', 'delegate')")] string? typeKindFilter = null)
    {
        logger.LogInformation("ListTypes called for: {AssemblyPath}, publicOnly: {PublicOnly}, namespace: {Namespace}, kind: {Kind}",
            assemblyPath, publicOnly, namespaceFilter ?? "all", typeKindFilter ?? "all");

        try
        {
            var request = new ListTypesRequest
            {
                AssemblyPath = assemblyPath,
                PublicOnly = publicOnly,
                NamespaceFilter = namespaceFilter,
                TypeKindFilter = typeKindFilter
            };

            ListTypesResponse result = analysisService.ListTypes(request);
            return await Task.FromResult(JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing types");
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                success = false,
                types = Array.Empty<object>(),
                totalCount = 0
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
