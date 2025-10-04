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
public class FileOperationTools(
    FileOperationsService fileService,
    TypeResearchAttestationService attestationService)
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
    [Description("""
                 REQUIRED before creating code files: Attest that you have thoroughly researched all types, 
                 properties, and methods you plan to use in the code file.
                 
                 This creates a research token that you MUST provide when calling file_write for code files.
                 The token expires after 10 minutes and is consumed (one-time use) when you create the file.
                 
                 Supported code file extensions: .cs, .py, .js, .ts, .jsx, .tsx, .java, .go, .rs
                 """)]
    public string AttestCodeFileResearch(
        [Description("Path where the code file will be created - must be canonical")]
        string targetFilePath,
        [Description("Comma-separated list of external types/APIs you researched (e.g., 'AzureDiscoveryResult, ResourceGroupResource, SubscriptionResource')")]
        string typesResearched,
        [Description("Attestation: Must be exactly 'I have thoroughly researched all types and verified property names, method signatures, and constructor parameters'")]
        string attestation)
    {
        try
        {
            (bool success, string? token, string? error) = attestationService.CreateAttestation(
                targetFilePath, typesResearched, attestation);

            if (!success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = error,
                    hint = "The attestation text must match exactly. Copy and paste the required text from the parameter description."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                researchToken = token,
                targetFile = Path.GetFullPath(targetFilePath),
                typesResearched = typesResearched.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToArray(),
                expiresIn = "10 minutes",
                message = "Research attestation created. Use this token when calling file_write to create the code file.",
                nextStep = $"Call file_write with path='{targetFilePath}' and researchToken='{token}'"
            }, new JsonSerializerOptions { WriteIndented = true });
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
        bool createDirectories = true,
        [Description("Research token from attest_code_file_research (REQUIRED for code files: .cs, .py, .js, .ts, etc.)")]
        string? researchToken = null)
    {
        try
        {
            object result = await fileService.WriteFileAsync(
                path, content, encoding, createDirectories, researchToken);
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
