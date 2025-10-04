using AzureMcp.Services.DevOps;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.DevOps.Models;

namespace AzureMcp.Tools;

[McpServerToolType]
public class DevOpsTools(IDevOpsService devOpsService)
{
    [McpServerTool]
    [Description("List all Azure DevOps projects")]
    public async Task<string> ListProjectsAsync()
    {
        try
        {
            IEnumerable<ProjectDto> projects = await devOpsService.GetProjectsAsync();
            return JsonSerializer.Serialize(new { success = true, projects = projects.ToArray() }, 
                SerializerOptions.JsonOptionsIndented);
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
            ProjectDto? project = await devOpsService.GetProjectAsync(projectName);
            return JsonSerializer.Serialize(new { success = true, project }, 
                SerializerOptions.JsonOptionsIndented);
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
            WorkItemDto? workItem = await devOpsService.GetWorkItemAsync(id);
            return JsonSerializer.Serialize(new { success = true, workItem }, 
                SerializerOptions.JsonOptionsIndented);
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
            IEnumerable<WorkItemDto> workItems = await devOpsService.GetWorkItemsAsync(projectName, wiql);
            return JsonSerializer.Serialize(new { success = true, workItems = workItems.ToArray() }, 
                SerializerOptions.JsonOptionsIndented);
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

            WorkItemDto workItem = await devOpsService.CreateWorkItemAsync(projectName, workItemType, title, fields);
            return JsonSerializer.Serialize(new { success = true, workItem }, 
                SerializerOptions.JsonOptionsIndented);
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
            IEnumerable<RepositoryDto> repositories = await devOpsService.GetRepositoriesAsync(projectName);
            return JsonSerializer.Serialize(new { success = true, repositories = repositories.ToArray() }, 
                SerializerOptions.JsonOptionsIndented);
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
            RepositoryDto? repository = await devOpsService.GetRepositoryAsync(projectName, repositoryName);
            return JsonSerializer.Serialize(new { success = true, repository }, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetRepository");
        }
    }
    
    #region Pipeline Tools

    [McpServerTool]
    [Description("List build definitions (pipelines) in a project")]
    public async Task<string> ListBuildDefinitionsAsync(
        [Description("Project name")] string projectName)
    {
        try
        {
            IEnumerable<BuildDefinitionDto> definitions = await devOpsService.GetBuildDefinitionsAsync(projectName);
            return JsonSerializer.Serialize(new { success = true, buildDefinitions = definitions.ToArray() }, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListBuildDefinitions");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific build definition")]
    public async Task<string> GetBuildDefinitionAsync(
        [Description("Project name")] string projectName,
        [Description("Build definition ID")] int definitionId)
    {
        try
        {
            BuildDefinitionDto? definition = await devOpsService.GetBuildDefinitionAsync(projectName, definitionId);
            return JsonSerializer.Serialize(new { success = true, buildDefinition = definition }, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBuildDefinition");
        }
    }

    [McpServerTool]
    [Description("List recent builds in a project")]
    public async Task<string> ListBuildsAsync(
        [Description("Project name")] string projectName,
        [Description("Optional build definition ID to filter")] int? definitionId = null,
        [Description("Maximum number of builds to return")] int? top = 10)
    {
        try
        {
            IEnumerable<BuildDto> builds = await devOpsService.GetBuildsAsync(projectName, definitionId, top);
            return JsonSerializer.Serialize(new { success = true, builds = builds.ToArray() }, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListBuilds");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific build")]
    public async Task<string> GetBuildAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId)
    {
        try
        {
            BuildDto? build = await devOpsService.GetBuildAsync(projectName, buildId);
            return JsonSerializer.Serialize(new { success = true, build }, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBuild");
        }
    }

    [McpServerTool]
    [Description("Queue/trigger a build")]
    public async Task<string> QueueBuildAsync(
        [Description("Project name")] string projectName,
        [Description("Build definition ID")] int definitionId,
        [Description("Optional branch to build")] string? branch = null)
    {
        try
        {
            BuildDto build = await devOpsService.QueueBuildAsync(projectName, definitionId, branch);
            return JsonSerializer.Serialize(new { success = true, queuedBuild = build }, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "QueueBuild");
        }
    }

    [McpServerTool]
    [Description("List release definitions in a project")]
    public async Task<string> ListReleaseDefinitionsAsync(
        [Description("Project name")] string projectName)
    {
        try
        {
            IEnumerable<ReleaseDefinitionDto> definitions = await devOpsService.GetReleaseDefinitionsAsync(projectName);
            return JsonSerializer.Serialize(new { success = true, releaseDefinitions = definitions.ToArray() }, 
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListReleaseDefinitions");
        }
    }

    #endregion

    #region YAML File Tools

    [McpServerTool]
    [Description("Get the content of a file from a repository (useful for YAML pipeline files)")]
    public async Task<string> GetRepositoryFileAsync(
        [Description("Project name")] string projectName,
        [Description("Repository name")] string repositoryName,
        [Description("File path (e.g., azure-pipelines.yml, .github/workflows/build.yml) - must be canonical")] string filePath,
        [Description("Optional branch name (defaults to main/master)")] string? branch = null)
    {
        try
        {
            string? content = await devOpsService.GetRepositoryFileContentAsync(projectName, repositoryName, filePath, branch);
            return JsonSerializer.Serialize(new { 
                success = true, 
                filePath, 
                content,
                branch = branch ?? "default",
                repository = repositoryName,
                project = projectName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetRepositoryFile");
        }
    }

    [McpServerTool]
    [Description("Update a file in a repository (useful for updating YAML pipeline files)")]
    public async Task<string> UpdateRepositoryFileAsync(
        [Description("Project name")] string projectName,
        [Description("Repository name")] string repositoryName,
        [Description("File path (e.g., azure-pipelines.yml) - must be canonical")] string filePath,
        [Description("New file content")] string content,
        [Description("Commit message")] string commitMessage,
        [Description("Optional branch name (defaults to main)")] string? branch = null)
    {
        try
        {
            bool success = await devOpsService.UpdateRepositoryFileAsync(projectName, repositoryName, filePath, content, commitMessage, branch);
            return JsonSerializer.Serialize(new { 
                success, 
                message = success ? "File updated successfully" : "File update failed",
                filePath,
                repository = repositoryName,
                project = projectName,
                commitMessage
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateRepositoryFile");
        }
    }

    [McpServerTool]
    [Description("Find all YAML pipeline files in a repository")]
    public async Task<string> FindYamlPipelineFilesAsync(
        [Description("Project name")] string projectName,
        [Description("Repository name")] string repositoryName)
    {
        try
        {
            IEnumerable<string> yamlFiles = await devOpsService.FindYamlPipelineFilesAsync(projectName, repositoryName);
            return JsonSerializer.Serialize(new { 
                success = true, 
                yamlFiles = yamlFiles.ToArray(),
                repository = repositoryName,
                project = projectName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "FindYamlPipelineFiles");
        }
    }

    [McpServerTool]
    [Description("Get the YAML content for a specific pipeline definition")]
    public async Task<string> GetPipelineYamlAsync(
        [Description("Project name")] string projectName,
        [Description("Pipeline definition ID")] int definitionId)
    {
        try
        {
            string? yamlContent = await devOpsService.GetPipelineYamlAsync(projectName, definitionId);
            return JsonSerializer.Serialize(new { 
                success = true, 
                definitionId,
                yamlContent,
                project = projectName
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetPipelineYaml");
        }
    }

    [McpServerTool]
    [Description("Update the YAML content for a pipeline definition")]
    public async Task<string> UpdatePipelineYamlAsync(
        [Description("Project name")] string projectName,
        [Description("Pipeline definition ID")] int definitionId,
        [Description("New YAML content")] string yamlContent,
        [Description("Commit message")] string commitMessage)
    {
        try
        {
            bool success = await devOpsService.UpdatePipelineYamlAsync(projectName, definitionId, yamlContent, commitMessage);
            return JsonSerializer.Serialize(new { 
                success, 
                message = success ? "Pipeline YAML updated successfully" : "Pipeline YAML update failed",
                definitionId,
                project = projectName,
                commitMessage
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdatePipelineYaml");
        }
    }

    #endregion
    
    #region Build Log Tools
    
    [McpServerTool]
    [Description("Search build logs using regex patterns with context lines")]
    public async Task<string> SearchBuildLogsWithRegexAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId,
        [Description("Regex pattern to search for (e.g., 'kendo|license|TKL\\d+' for Kendo issues)")] string regexPattern,
        [Description("Number of context lines around matches (default: 3)")] int contextLines = 3,
        [Description("Case sensitive search (default: false)")] bool caseSensitive = false,
        [Description("Maximum matches to return (default: 50)")] int maxMatches = 50)
    {
        try
        {
            string result = await devOpsService.SearchBuildLogsWithRegexAsync(
                projectName, buildId, regexPattern, contextLines, caseSensitive, maxMatches);
            return result;
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SearchBuildLogsWithRegex");
        }
    }

    [McpServerTool]
    [Description("Get build logs for a specific build")]
    public async Task<string> GetBuildLogsAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId)
    {
        try
        {
            IEnumerable<BuildLogDto> logs = await devOpsService.GetBuildLogsAsync(projectName, buildId);
            return JsonSerializer.Serialize(new { 
                success = true, 
                buildId,
                project = projectName,
                logs = logs.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBuildLogs");
        }
    }

    [McpServerTool]
    [Description("Get the complete build log content for a specific build")]
    public async Task<string> GetCompleteBuildLogAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId)
    {
        try
        {
            string logContent = await devOpsService.GetCompleteBuildLogAsync(projectName, buildId);
            return JsonSerializer.Serialize(new { 
                success = true, 
                buildId,
                project = projectName,
                logContent
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetCompleteBuildLog");
        }
    }

    [McpServerTool]
    [Description("Get build timeline with step-by-step execution details")]
    public async Task<string> GetBuildTimelineAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId)
    {
        try
        {
            BuildTimelineDto? timeline = await devOpsService.GetBuildTimelineAsync(projectName, buildId);
            return JsonSerializer.Serialize(new { 
                success = true, 
                buildId,
                project = projectName,
                timeline
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBuildTimeline");
        }
    }

    [McpServerTool]
    [Description("Get detailed logs for all build steps with error/warning extraction")]
    public async Task<string> GetBuildStepLogsAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId)
    {
        try
        {
            List<BuildStepLogDto> stepLogs = (await devOpsService.GetBuildStepLogsAsync(projectName, buildId)).ToList();
            return JsonSerializer.Serialize(new { 
                success = true, 
                buildId,
                project = projectName,
                stepLogs = stepLogs.ToArray(),
                summary = new {
                    totalSteps = stepLogs.Count,
                    stepsWithErrors = stepLogs.Count(s => s.ErrorMessages.Count != 0),
                    stepsWithWarnings = stepLogs.Count(s => s.WarningMessages.Count != 0)
                }
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBuildStepLogs");
        }
    }

    [McpServerTool]
    [Description("Get log content for a specific build log ID")]
    public async Task<string> GetBuildLogContentAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId,
        [Description("Log ID")] int logId)
    {
        try
        {
            BuildLogContentDto? logContent = await devOpsService.GetBuildLogContentAsync(projectName, buildId, logId);
            return JsonSerializer.Serialize(new { 
                success = true, 
                buildId,
                logId,
                project = projectName,
                logContent
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBuildLogContent");
        }
    }

    [McpServerTool]
    [Description("Get log content for a specific build task/step")]
    public async Task<string> GetBuildTaskLogAsync(
        [Description("Project name")] string projectName,
        [Description("Build ID")] int buildId,
        [Description("Task ID")] string taskId)
    {
        try
        {
            BuildLogContentDto? taskLog = await devOpsService.GetBuildTaskLogAsync(projectName, buildId, taskId);
            return JsonSerializer.Serialize(new { 
                success = true, 
                buildId,
                taskId,
                project = projectName,
                taskLog
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetBuildTaskLog");
        }
    }

    #endregion
    
    private static string HandleError(Exception ex, string operation)
    {
        var error = new
        {
            success = false,
            error = $"Error during {operation}",
            details = ex.Message,
            errorType = ex.GetType().Name
        };

        return JsonSerializer.Serialize(error, SerializerOptions.JsonOptionsIndented);
    }
}
