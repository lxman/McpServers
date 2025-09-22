using AzureMcp.Services.DevOps;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Services.DevOps.Models;

namespace AzureMcp.Tools;

[McpServerToolType]
public class DevOpsTools
{
    private readonly IDevOpsService _devOpsService;

    public DevOpsTools(IDevOpsService devOpsService)
    {
        _devOpsService = devOpsService;
    }

    [McpServerTool]
    [Description("List all Azure DevOps projects")]
    public async Task<string> ListProjectsAsync()
    {
        try
        {
            var projects = await _devOpsService.GetProjectsAsync();
            return JsonSerializer.Serialize(new { success = true, projects = projects.ToArray() }, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListProjects");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Azure DevOps project")]
    public async Task<string> GetProjectAsync(
        [Description("Project name")] string projectName)
    {
        try
        {
            var project = await _devOpsService.GetProjectAsync(projectName);
            return JsonSerializer.Serialize(new { success = true, project }, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetProject");
        }
    }

    [McpServerTool]
    [Description("Get a work item by ID")]
    public async Task<string> GetWorkItemAsync(
        [Description("Work item ID")] int id)
    {
        try
        {
            var workItem = await _devOpsService.GetWorkItemAsync(id);
            return JsonSerializer.Serialize(new { success = true, workItem }, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetWorkItem");
        }
    }

    [McpServerTool]
    [Description("Get work items from a project with optional WIQL query")]
    public async Task<string> GetWorkItemsAsync(
        [Description("Project name")] string projectName,
        [Description("Optional WIQL query")] string? wiql = null)
    {
        try
        {
            var workItems = await _devOpsService.GetWorkItemsAsync(projectName, wiql);
            return JsonSerializer.Serialize(new { success = true, workItems = workItems.ToArray() }, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetWorkItems");
        }
    }

    [McpServerTool]
    [Description("Create a new work item")]
    public async Task<string> CreateWorkItemAsync(
        [Description("Project name")] string projectName,
        [Description("Work item type (e.g., Bug, Task, User Story)")] string workItemType,
        [Description("Title of the work item")] string title,
        [Description("Optional description")] string? description = null,
        [Description("Optional assigned to user")] string? assignedTo = null,
        [Description("Optional priority (1-4)")] int? priority = null)
    {
        try
        {
            var fields = new Dictionary<string, object>();
            
            if (!string.IsNullOrEmpty(description))
                fields["System.Description"] = description;
            
            if (!string.IsNullOrEmpty(assignedTo))
                fields["System.AssignedTo"] = assignedTo;
            
            if (priority.HasValue)
                fields["Microsoft.VSTS.Common.Priority"] = priority.Value;

            var workItem = await _devOpsService.CreateWorkItemAsync(projectName, workItemType, title, fields);
            return JsonSerializer.Serialize(new { success = true, workItem }, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateWorkItem");
        }
    }

    [McpServerTool]
    [Description("List repositories in a project")]
    public async Task<string> ListRepositoriesAsync(
        [Description("Project name")] string projectName)
    {
        try
        {
            var repositories = await _devOpsService.GetRepositoriesAsync(projectName);
            return JsonSerializer.Serialize(new { success = true, repositories = repositories.ToArray() }, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListRepositories");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific repository")]
    public async Task<string> GetRepositoryAsync(
        [Description("Project name")] string projectName,
        [Description("Repository name")] string repositoryName)
    {
        try
        {
            var repository = await _devOpsService.GetRepositoryAsync(projectName, repositoryName);
            return JsonSerializer.Serialize(new { success = true, repository }, 
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetRepository");
        }
    }

    private static string HandleError(Exception ex, string operation)
    {
        var error = new
        {
            success = false,
            error = $"Error during {operation}",
            details = ex.Message,
            errorType = ex.GetType().Name
        };

        return JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true });
    }
}
