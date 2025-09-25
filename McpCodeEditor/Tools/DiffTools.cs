using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools;

[McpServerToolType]
public class DiffTools(DiffService diffService)
{
    #region Diff Operations

    [McpServerTool]
    [Description("Generate a diff between two texts or files")]
    public async Task<string> DiffGenerateAsync(
        [Description("Path to original file (alternative to originalContent)")]
        string? originalPath = null,
        [Description("Path to modified file (alternative to modifiedContent)")]
        string? modifiedPath = null,
        [Description("Original content (alternative to originalPath)")]
        string? originalContent = null,
        [Description("Modified content (alternative to modifiedPath)")]
        string? modifiedContent = null,
        [Description("Diff format (unified, side-by-side, inline)")]
        string format = "unified",
        [Description("Number of context lines")]
        int contextLines = 3)
    {
        try
        {
            var result = await diffService.GenerateAsync(originalPath, modifiedPath, originalContent, modifiedContent, format, contextLines);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion
}
