using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools;

/// <summary>
/// File operation tools following Single Responsibility Principle.
/// Handles: Read, Write, List, Delete, and Search operations on files.
/// </summary>
[McpServerToolType]
public class FileOperationTools(FileOperationsService fileService)
{
    [McpServerTool]
    [Description("Read the contents of a file")]
    public async Task<string> FileReadAsync(
        [Description("Path to the file to read - must be canonical")]
        string path,
        [Description("File encoding (utf-8, ascii, etc.)")]
        string encoding = "utf-8")
    {
        try
        {
            object result = await fileService.ReadFileAsync(path, encoding);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Write content to a file")]
    public async Task<string> FileWriteAsync(
        [Description("Path to the file to write - must be canonical")]
        string path,
        [Description("Content to write to the file")]
        string content,
        [Description("File encoding")]
        string encoding = "utf-8",
        [Description("Create parent directories if they don't exist")]
        bool createDirectories = true)
    {
        try
        {
            object result = await fileService.WriteFileAsync(path, content, encoding, createDirectories);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("List files and directories")]
    public async Task<string> FileListAsync(
        [Description("Directory path to list - must be canonical")]
        string path = ".",
        [Description("List files recursively")]
        bool recursive = false,
        [Description("Include hidden files")]
        bool includeHidden = false,
        [Description("File pattern to match (glob pattern)")]
        string? pattern = null)
    {
        try
        {
            object result = await fileService.ListFilesAsync(path, recursive, includeHidden, pattern);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Delete a file or directory")]
    public async Task<string> FileDeleteAsync(
        [Description("Path to delete - must be canonical")]
        string path,
        [Description("Delete directories recursively")]
        bool recursive = false)
    {
        try
        {
            object result = await fileService.DeleteAsync(path, recursive);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Search for text within files")]
    public async Task<string> FileSearchAsync(
        [Description("Text to search for")]
        string query,
        [Description("Directory to search in - must be canonical")]
        string path = ".",
        [Description("File pattern to search in")]
        string filePattern = "*",
        [Description("Case sensitive search")]
        bool caseSensitive = false,
        [Description("Use regular expressions")]
        bool regex = false,
        [Description("Maximum number of results")]
        int maxResults = 100)
    {
        try
        {
            object result = await fileService.SearchAsync(query, path, filePattern, caseSensitive, regex, maxResults);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
