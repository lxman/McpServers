using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools;

[McpServerToolType]
public partial class CodeAnalysisTools(CodeAnalysisService codeService)
{
    [McpServerTool]
    [Description("Analyze code for syntax, diagnostics, and structure")]
    public async Task<string> CodeAnalyzeAsync(
        [Description("Path to the code file (alternative to content)")]
        string? path = null,
        [Description("Code content (alternative to path)")]
        string? content = null,
        [Description("Programming language (auto-detected if not specified)")]
        string? language = null,
        [Description("Include diagnostic information")]
        bool includeDiagnostics = true,
        [Description("Include symbol information")]
        bool includeSymbols = true,
        [Description("Include code metrics")]
        bool includeMetrics = false)
    {
        try
        {
            object result = await codeService.AnalyzeAsync(path, content, language, includeDiagnostics, includeSymbols, includeMetrics);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Format code according to language conventions")]
    public static async Task<string> CodeFormatAsync(
        [Description("Path to the code file (alternative to content)")]
        string? path = null,
        [Description("Code content (alternative to path)")]
        string? content = null,
        [Description("Programming language")]
        string? language = null,
        [Description("Write formatted code back to file")]
        bool writeToFile = false)
    {
        try
        {
            object result = await CodeAnalysisService.FormatAsync(path, content, language, writeToFile);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
