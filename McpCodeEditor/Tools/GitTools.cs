using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools;

[McpServerToolType]
public partial class GitTools(GitService gitService)
{
    [McpServerTool]
    [Description("Show the working tree status of a Git repository")]
    public async Task<string> GitStatusAsync(
        [Description("Path to the Git repository (optional, uses current workspace if not specified)")]
        string? repositoryPath = null)
    {
        try
        {
            var result = await gitService.GetStatusAsync(repositoryPath);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Show changes between commits, commit and working tree, etc.")]
    public async Task<string> GitDiffAsync(
        [Description("Path to the Git repository (optional, uses current workspace if not specified)")]
        string? repositoryPath = null,
        [Description("From commit SHA or reference (optional, defaults to HEAD)")]
        string? fromCommit = null,
        [Description("To commit SHA or reference (optional, defaults to working directory)")]
        string? toCommit = null,
        [Description("Filter by specific file path (optional)")]
        string? filePath = null,
        [Description("Number of context lines around changes")]
        int contextLines = 3)
    {
        try
        {
            var result = await gitService.GetDiffAsync(repositoryPath, fromCommit, toCommit, filePath, contextLines);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Show commit logs")]
    public async Task<string> GitLogAsync(
        [Description("Path to the Git repository (optional, uses current workspace if not specified)")]
        string? repositoryPath = null,
        [Description("Maximum number of commits to show")]
        int maxCount = 20,
        [Description("Filter commits that touched this file path (optional)")]
        string? filePath = null,
        [Description("Show commits from specific branch (optional, defaults to current branch)")]
        string? branch = null)
    {
        try
        {
            var result = await gitService.GetLogAsync(repositoryPath, maxCount, filePath, branch);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Show what revision and author last modified each line of a file")]
    public async Task<string> GitBlameAsync(
        [Description("Path to the file to blame")]
        string filePath,
        [Description("Path to the Git repository (optional, uses current workspace if not specified)")]
        string? repositoryPath = null)
    {
        try
        {
            var result = await gitService.GetBlameAsync(filePath, repositoryPath);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("List, create, or delete branches")]
    public async Task<string> GitBranchListAsync(
        [Description("Path to the Git repository (optional, uses current workspace if not specified)")]
        string? repositoryPath = null,
        [Description("Include remote branches in the listing")]
        bool includeRemote = true)
    {
        try
        {
            var result = await gitService.GetBranchesAsync(repositoryPath, includeRemote);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
