using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using CSharpAnalyzer.Core.Models.Reflection;
using CSharpAnalyzer.Core.Services.Reflection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CSharpAnalyzerMcp.McpTools;

/// <summary>
/// MCP tools for .NET assembly reflection and analysis
/// </summary>
[McpServerToolType]
public class ReflectionTools(AssemblyAnalysisService analysisService, ILogger<ReflectionTools> logger)
{
    [McpServerTool, DisplayName("get_assembly_info")]
    [Description("Get detailed metadata and information about a .NET assembly. See skills/csharp/get-assembly-info.md only when using this tool")]
    public string GetAssemblyInfo(string assemblyPath)
    {
        try
        {
            logger.LogDebug("Getting assembly info for: {AssemblyPath}", assemblyPath);
            AssemblyInfoResponse result = analysisService.GetAssemblyInfo(assemblyPath);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting assembly info");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("list_types")]
    [Description("List all types (classes, interfaces, enums, structs) in a .NET assembly. See skills/csharp/list-types.md only when using this tool")]
    public string ListTypes(
        string assemblyPath,
        bool publicOnly = false,
        string? namespaceFilter = null,
        string? typeKindFilter = null)
    {
        try
        {
            logger.LogDebug(
                "Listing types for: {AssemblyPath}, publicOnly: {PublicOnly}, namespace: {Namespace}, kind: {Kind}",
                assemblyPath, publicOnly, namespaceFilter ?? "all", typeKindFilter ?? "all");

            var request = new ListTypesRequest
            {
                AssemblyPath = assemblyPath,
                PublicOnly = publicOnly,
                NamespaceFilter = namespaceFilter,
                TypeKindFilter = typeKindFilter
            };

            ListTypesResponse result = analysisService.ListTypes(request);
            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing types");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                types = Array.Empty<object>(),
                totalCount = 0
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
}