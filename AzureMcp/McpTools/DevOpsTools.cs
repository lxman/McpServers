using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using AzureServer.Core.Services.DevOps;
using AzureServer.Core.Services.DevOps.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure DevOps operations
/// </summary>
[McpServerToolType]
public class DevOpsTools(
    IDevOpsService devOpsService,
    ILogger<DevOpsTools> logger)
{
    #region Projects

    [McpServerTool, DisplayName("get_projects")]
    [Description("Get DevOps projects. See skills/azure/devops/get-projects.md only when using this tool")]
    public async Task<string> GetProjects()
    {
        try
        {
            logger.LogDebug("Getting DevOps projects");
            IEnumerable<ProjectDto> projects = await devOpsService.GetProjectsAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                projects = projects.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting projects");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_project")]
    [Description("Get DevOps project. See skills/azure/devops/get-project.md only when using this tool")]
    public async Task<string> GetProject(string projectName)
    {
        try
        {
            logger.LogDebug("Getting project {ProjectName}", projectName);
            ProjectDto? project = await devOpsService.GetProjectAsync(projectName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                project
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Work Items

    [McpServerTool, DisplayName("get_work_item")]
    [Description("Get work item. See skills/azure/devops/get-work-item.md only when using this tool")]
    public async Task<string> GetWorkItem(int id)
    {
        try
        {
            logger.LogDebug("Getting work item {Id}", id);
            WorkItemDto? workItem = await devOpsService.GetWorkItemAsync(id);

            return JsonSerializer.Serialize(new
            {
                success = true,
                workItem
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting work item {Id}", id);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_work_items")]
    [Description("Get work items. See skills/azure/devops/get-work-items.md only when using this tool")]
    public async Task<string> GetWorkItems(string projectName, string? wiql = null)
    {
        try
        {
            logger.LogDebug("Getting work items for project {ProjectName}", projectName);
            IEnumerable<WorkItemDto> workItems = await devOpsService.GetWorkItemsAsync(projectName, wiql);

            return JsonSerializer.Serialize(new
            {
                success = true,
                workItems = workItems.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting work items for project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("create_work_item")]
    [Description("Create work item. See skills/azure/devops/create-work-item.md only when using this tool")]
    public async Task<string> CreateWorkItem(
        string projectName,
        string workItemType,
        string title,
        Dictionary<string, object>? fields = null)
    {
        try
        {
            logger.LogDebug("Creating work item in project {ProjectName}", projectName);
            WorkItemDto workItem = await devOpsService.CreateWorkItemAsync(projectName, workItemType, title, fields);

            return JsonSerializer.Serialize(new
            {
                success = true,
                workItem
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating work item in project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Repositories

    [McpServerTool, DisplayName("get_repositories")]
    [Description("Get repositories. See skills/azure/devops/get-repositories.md only when using this tool")]
    public async Task<string> GetRepositories(string projectName)
    {
        try
        {
            logger.LogDebug("Getting repositories for project {ProjectName}", projectName);
            IEnumerable<RepositoryDto> repositories = await devOpsService.GetRepositoriesAsync(projectName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repositories = repositories.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting repositories for project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_repository")]
    [Description("Get repository. See skills/azure/devops/get-repository.md only when using this tool")]
    public async Task<string> GetRepository(string projectName, string repositoryName)
    {
        try
        {
            logger.LogDebug("Getting repository {RepositoryName}", repositoryName);
            RepositoryDto? repository = await devOpsService.GetRepositoryAsync(projectName, repositoryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                repository
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_repository_file")]
    [Description("Get repository file content. See skills/azure/devops/get-file.md only when using this tool")]
    public async Task<string> GetRepositoryFileContent(
        string projectName,
        string repositoryName,
        string filePath,
        string? branch = null)
    {
        try
        {
            logger.LogDebug("Getting file content for {FilePath}", filePath);
            string? content = await devOpsService.GetRepositoryFileContentAsync(projectName, repositoryName, filePath, branch);

            return JsonSerializer.Serialize(new
            {
                success = true,
                content
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file content for {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("update_repository_file")]
    [Description("Update repository file. See skills/azure/devops/update-file.md only when using this tool")]
    public async Task<string> UpdateRepositoryFile(
        string projectName,
        string repositoryName,
        string filePath,
        string content,
        string commitMessage,
        string? branch = null)
    {
        try
        {
            logger.LogDebug("Updating file {FilePath}", filePath);
            bool success = await devOpsService.UpdateRepositoryFileAsync(projectName, repositoryName, filePath, content, commitMessage, branch);

            return JsonSerializer.Serialize(new
            {
                success
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating file {FilePath}", filePath);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("find_yaml_pipelines")]
    [Description("Find YAML pipeline files. See skills/azure/devops/find-yaml-pipelines.md only when using this tool")]
    public async Task<string> FindYamlPipelineFiles(string projectName, string repositoryName)
    {
        try
        {
            logger.LogDebug("Finding YAML pipeline files in repository {RepositoryName}", repositoryName);
            IEnumerable<string> files = await devOpsService.FindYamlPipelineFilesAsync(projectName, repositoryName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                files = files.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding YAML pipeline files in repository {RepositoryName}", repositoryName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Build Definitions

    [McpServerTool, DisplayName("get_build_definitions")]
    [Description("Get build definitions. See skills/azure/devops/get-build-definitions.md only when using this tool")]
    public async Task<string> GetBuildDefinitions(string projectName)
    {
        try
        {
            logger.LogDebug("Getting build definitions for project {ProjectName}", projectName);
            IEnumerable<BuildDefinitionDto> definitions = await devOpsService.GetBuildDefinitionsAsync(projectName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                buildDefinitions = definitions.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build definitions for project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_build_definition")]
    [Description("Get build definition. See skills/azure/devops/get-build-definition.md only when using this tool")]
    public async Task<string> GetBuildDefinition(string projectName, int definitionId)
    {
        try
        {
            logger.LogDebug("Getting build definition {DefinitionId}", definitionId);
            BuildDefinitionDto? definition = await devOpsService.GetBuildDefinitionAsync(projectName, definitionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                buildDefinition = definition
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build definition {DefinitionId}", definitionId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_pipeline_yaml")]
    [Description("Get pipeline YAML. See skills/azure/devops/get-pipeline-yaml.md only when using this tool")]
    public async Task<string> GetPipelineYaml(string projectName, int definitionId)
    {
        try
        {
            logger.LogDebug("Getting pipeline YAML for definition {DefinitionId}", definitionId);
            string? yaml = await devOpsService.GetPipelineYamlAsync(projectName, definitionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                yaml
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting pipeline YAML for definition {DefinitionId}", definitionId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("update_pipeline_yaml")]
    [Description("Update pipeline YAML. See skills/azure/devops/update-pipeline-yaml.md only when using this tool")]
    public async Task<string> UpdatePipelineYaml(
        string projectName,
        int definitionId,
        string yamlContent,
        string commitMessage)
    {
        try
        {
            logger.LogDebug("Updating pipeline YAML for definition {DefinitionId}", definitionId);
            bool success = await devOpsService.UpdatePipelineYamlAsync(projectName, definitionId, yamlContent, commitMessage);

            return JsonSerializer.Serialize(new
            {
                success
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating pipeline YAML for definition {DefinitionId}", definitionId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Builds

    [McpServerTool, DisplayName("get_builds")]
    [Description("Get builds. See skills/azure/devops/get-builds.md only when using this tool")]
    public async Task<string> GetBuilds(string projectName, int? definitionId = null, int? top = null)
    {
        try
        {
            logger.LogDebug("Getting builds for project {ProjectName}", projectName);
            IEnumerable<BuildDto> builds = await devOpsService.GetBuildsAsync(projectName, definitionId, top);

            return JsonSerializer.Serialize(new
            {
                success = true,
                builds = builds.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting builds for project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_build")]
    [Description("Get build. See skills/azure/devops/get-build.md only when using this tool")]
    public async Task<string> GetBuild(string projectName, int buildId)
    {
        try
        {
            logger.LogDebug("Getting build {BuildId}", buildId);
            BuildDto? build = await devOpsService.GetBuildAsync(projectName, buildId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                build
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build {BuildId}", buildId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("queue_build")]
    [Description("Queue build. See skills/azure/devops/queue-build.md only when using this tool")]
    public async Task<string> QueueBuild(string projectName, int definitionId, string? branch = null)
    {
        try
        {
            logger.LogDebug("Queueing build for definition {DefinitionId}", definitionId);
            BuildDto build = await devOpsService.QueueBuildAsync(projectName, definitionId, branch);

            return JsonSerializer.Serialize(new
            {
                success = true,
                build
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing build for definition {DefinitionId}", definitionId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_build_logs")]
    [Description("Get build logs. See skills/azure/devops/get-build-logs.md only when using this tool")]
    public async Task<string> GetBuildLogs(string projectName, int buildId)
    {
        try
        {
            logger.LogDebug("Getting build logs for build {BuildId}", buildId);
            IEnumerable<BuildLogDto> logs = await devOpsService.GetBuildLogsAsync(projectName, buildId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                logs = logs.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build logs for build {BuildId}", buildId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_build_log_content")]
    [Description("Get build log content. See skills/azure/devops/get-build-log.md only when using this tool")]
    public async Task<string> GetBuildLogContent(string projectName, int buildId, int logId)
    {
        try
        {
            logger.LogDebug("Getting build log content for log {LogId}", logId);
            BuildLogContentDto? logContent = await devOpsService.GetBuildLogContentAsync(projectName, buildId, logId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                logContent
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build log content for log {LogId}", logId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_build_timeline")]
    [Description("Get build timeline. See skills/azure/devops/get-build-timeline.md only when using this tool")]
    public async Task<string> GetBuildTimeline(string projectName, int buildId)
    {
        try
        {
            logger.LogDebug("Getting build timeline for build {BuildId}", buildId);
            BuildTimelineDto? timeline = await devOpsService.GetBuildTimelineAsync(projectName, buildId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                timeline
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build timeline for build {BuildId}", buildId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_build_step_logs")]
    [Description("Get build step logs. See skills/azure/devops/get-step-logs.md only when using this tool")]
    public async Task<string> GetBuildStepLogs(string projectName, int buildId)
    {
        try
        {
            logger.LogDebug("Getting build step logs for build {BuildId}", buildId);
            IEnumerable<BuildStepLogDto> stepLogs = await devOpsService.GetBuildStepLogsAsync(projectName, buildId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                stepLogs = stepLogs.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build step logs for build {BuildId}", buildId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_complete_build_log")]
    [Description("Get complete build log. See skills/azure/devops/get-complete-log.md only when using this tool")]
    public async Task<string> GetCompleteBuildLog(string projectName, int buildId)
    {
        try
        {
            logger.LogDebug("Getting complete build log for build {BuildId}", buildId);
            string log = await devOpsService.GetCompleteBuildLogAsync(projectName, buildId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                log
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting complete build log for build {BuildId}", buildId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_build_task_log")]
    [Description("Get build task log. See skills/azure/devops/get-task-log.md only when using this tool")]
    public async Task<string> GetBuildTaskLog(string projectName, int buildId, string taskId)
    {
        try
        {
            logger.LogDebug("Getting build task log for task {TaskId}", taskId);
            BuildLogContentDto? taskLog = await devOpsService.GetBuildTaskLogAsync(projectName, buildId, taskId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                taskLog
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build task log for task {TaskId}", taskId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("search_build_logs")]
    [Description("Search build logs with regex. See skills/azure/devops/search-logs.md only when using this tool")]
    public async Task<string> SearchBuildLogsWithRegex(
        string projectName,
        int buildId,
        string regexPattern,
        int contextLines = 3,
        bool caseSensitive = false,
        int maxMatches = 50)
    {
        try
        {
            logger.LogDebug("Searching build logs for build {BuildId}", buildId);
            string result = await devOpsService.SearchBuildLogsWithRegexAsync(
                projectName, buildId, regexPattern, contextLines, caseSensitive, maxMatches);

            return JsonSerializer.Serialize(new
            {
                success = true,
                result
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching build logs for build {BuildId}", buildId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Releases

    [McpServerTool, DisplayName("get_release_definitions")]
    [Description("Get release definitions. See skills/azure/devops/get-release-definitions.md only when using this tool")]
    public async Task<string> GetReleaseDefinitions(string projectName)
    {
        try
        {
            logger.LogDebug("Getting release definitions for project {ProjectName}", projectName);
            IEnumerable<ReleaseDefinitionDto> definitions = await devOpsService.GetReleaseDefinitionsAsync(projectName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                releaseDefinitions = definitions.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting release definitions for project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_release_definition")]
    [Description("Get release definition. See skills/azure/devops/get-release-definition.md only when using this tool")]
    public async Task<string> GetReleaseDefinition(string projectName, int definitionId)
    {
        try
        {
            logger.LogDebug("Getting release definition {DefinitionId}", definitionId);
            ReleaseDefinitionDto? definition = await devOpsService.GetReleaseDefinitionAsync(projectName, definitionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                releaseDefinition = definition
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting release definition {DefinitionId}", definitionId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_releases")]
    [Description("Get releases. See skills/azure/devops/get-releases.md only when using this tool")]
    public async Task<string> GetReleases(string projectName, int? definitionId = null)
    {
        try
        {
            logger.LogDebug("Getting releases for project {ProjectName}", projectName);
            IEnumerable<ReleaseDto> releases = await devOpsService.GetReleasesAsync(projectName, definitionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                releases = releases.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting releases for project {ProjectName}", projectName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion
}