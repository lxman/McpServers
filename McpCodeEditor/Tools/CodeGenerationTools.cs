using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using McpCodeEditor.Models.Options;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools;

/// <summary>
/// MCP tools for code generation operations
/// </summary>
[McpServerToolType]
public partial class CodeGenerationTools(CodeGenerationService codeGenerationService)
{
    [McpServerTool]
    [Description("Generate a constructor from class fields and properties")]
    public async Task<string> CodeGenerateConstructorAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Name of the class to generate constructor for")]
        string className,
        [Description("Include all fields in constructor")]
        bool includeAllFields = true,
        [Description("Initialize properties in constructor")]
        bool initializeProperties = true,
        [Description("Access modifier for constructor")]
        string accessModifier = "public",
        [Description("Add null checks for reference types")]
        bool addNullChecks = true,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        try
        {
            var options = new GenerateConstructorOptions
            {
                IncludeAllFields = includeAllFields,
                InitializeProperties = initializeProperties,
                AccessModifier = accessModifier,
                AddNullChecks = addNullChecks
            };

            var result = await codeGenerationService.GenerateConstructorAsync(
                filePath, className, options, previewOnly);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Generate an Equals method for a class")]
    public async Task<string> CodeGenerateEqualsAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Name of the class to generate Equals method for")]
        string className,
        [Description("Include all fields in equality comparison")]
        bool includeAllFields = true,
        [Description("Include all properties in equality comparison")]
        bool includeAllProperties = true,
        [Description("Access modifier for the method")]
        string accessModifier = "public",
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        try
        {
            var options = new GenerateMethodOptions
            {
                IncludeAllFields = includeAllFields,
                IncludeAllProperties = includeAllProperties,
                AccessModifier = accessModifier
            };

            var result = await codeGenerationService.GenerateEqualsMethodAsync(
                filePath, className, options, previewOnly);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Generate a GetHashCode method for a class")]
    public async Task<string> CodeGenerateGetHashCodeAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Name of the class to generate GetHashCode method for")]
        string className,
        [Description("Include all fields in hash code calculation")]
        bool includeAllFields = true,
        [Description("Include all properties in hash code calculation")]
        bool includeAllProperties = true,
        [Description("Access modifier for the method")]
        string accessModifier = "public",
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        try
        {
            var options = new GenerateMethodOptions
            {
                IncludeAllFields = includeAllFields,
                IncludeAllProperties = includeAllProperties,
                AccessModifier = accessModifier
            };

            var result = await codeGenerationService.GenerateGetHashCodeMethodAsync(
                filePath, className, options, previewOnly);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
