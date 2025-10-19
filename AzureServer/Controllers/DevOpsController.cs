using AzureServer.Services.DevOps;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]/projects")]
public class DevOpsController(IDevOpsService devOpsService, ILogger<DevOpsController> logger) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult> GetProjects()
    {
        try
        {
            var projects = await devOpsService.GetProjectsAsync();
            return Ok(new { success = true, projects = projects.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting projects");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetProjects", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}")]
    public async Task<ActionResult> GetProject(string projectName)
    {
        try
        {
            var project = await devOpsService.GetProjectAsync(projectName);
            if (project is null)
                return NotFound(new { success = false, error = $"Project {projectName} not found" });

            return Ok(new { success = true, project });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetProject", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/workitems/{id}")]
    public async Task<ActionResult> GetWorkItem(string projectName, int id)
    {
        try
        {
            var workItem = await devOpsService.GetWorkItemAsync(id);
            if (workItem is null)
                return NotFound(new { success = false, error = $"Work item {id} not found" });

            return Ok(new { success = true, workItem });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting work item {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetWorkItem", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/workitems")]
    public async Task<ActionResult> GetWorkItems(string projectName, [FromQuery] string? wiql = null)
    {
        try
        {
            var workItems = await devOpsService.GetWorkItemsAsync(projectName, wiql);
            return Ok(new { success = true, workItems = workItems.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting work items for project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetWorkItems", type = ex.GetType().Name });
        }
    }

    [HttpPost("{projectName}/workitems")]
    public async Task<ActionResult> CreateWorkItem(
        string projectName,
        [FromBody] CreateWorkItemRequest request)
    {
        try
        {
            var workItem = await devOpsService.CreateWorkItemAsync(
                projectName, 
                request.WorkItemType, 
                request.Title, 
                request.Fields);
            return Ok(new { success = true, workItem });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating work item in project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "CreateWorkItem", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/repositories")]
    public async Task<ActionResult> GetRepositories(string projectName)
    {
        try
        {
            var repositories = await devOpsService.GetRepositoriesAsync(projectName);
            return Ok(new { success = true, repositories = repositories.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting repositories for project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetRepositories", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/repositories/{repositoryName}")]
    public async Task<ActionResult> GetRepository(string projectName, string repositoryName)
    {
        try
        {
            var repository = await devOpsService.GetRepositoryAsync(projectName, repositoryName);
            if (repository is null)
                return NotFound(new { success = false, error = $"Repository {repositoryName} not found" });

            return Ok(new { success = true, repository });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting repository {RepositoryName}", repositoryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetRepository", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/build-definitions")]
    public async Task<ActionResult> GetBuildDefinitions(string projectName)
    {
        try
        {
            var definitions = await devOpsService.GetBuildDefinitionsAsync(projectName);
            return Ok(new { success = true, buildDefinitions = definitions.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build definitions for project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildDefinitions", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/build-definitions/{definitionId:int}")]
    public async Task<ActionResult> GetBuildDefinition(string projectName, int definitionId)
    {
        try
        {
            var definition = await devOpsService.GetBuildDefinitionAsync(projectName, definitionId);
            if (definition is null)
                return NotFound(new { success = false, error = $"Build definition {definitionId} not found" });

            return Ok(new { success = true, buildDefinition = definition });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build definition {DefinitionId}", definitionId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildDefinition", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds")]
    public async Task<ActionResult> GetBuilds(
        string projectName,
        [FromQuery] int? definitionId = null,
        [FromQuery] int? top = null)
    {
        try
        {
            var builds = await devOpsService.GetBuildsAsync(projectName, definitionId, top);
            return Ok(new { success = true, builds = builds.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting builds for project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuilds", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds/{buildId:int}")]
    public async Task<ActionResult> GetBuild(string projectName, int buildId)
    {
        try
        {
            var build = await devOpsService.GetBuildAsync(projectName, buildId);
            if (build is null)
                return NotFound(new { success = false, error = $"Build {buildId} not found" });

            return Ok(new { success = true, build });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build {BuildId}", buildId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuild", type = ex.GetType().Name });
        }
    }

    [HttpPost("{projectName}/builds/queue")]
    public async Task<ActionResult> QueueBuild(
        string projectName,
        [FromBody] QueueBuildRequest request)
    {
        try
        {
            var build = await devOpsService.QueueBuildAsync(projectName, request.DefinitionId, request.Branch);
            return Ok(new { success = true, build });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing build for definition {DefinitionId}", request.DefinitionId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "QueueBuild", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/release-definitions")]
    public async Task<ActionResult> GetReleaseDefinitions(string projectName)
    {
        try
        {
            var definitions = await devOpsService.GetReleaseDefinitionsAsync(projectName);
            return Ok(new { success = true, releaseDefinitions = definitions.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting release definitions for project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetReleaseDefinitions", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/release-definitions/{definitionId}")]
    public async Task<ActionResult> GetReleaseDefinition(string projectName, int definitionId)
    {
        try
        {
            var definition = await devOpsService.GetReleaseDefinitionAsync(projectName, definitionId);
            if (definition is null)
                return NotFound(new { success = false, error = $"Release definition {definitionId} not found" });

            return Ok(new { success = true, releaseDefinition = definition });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting release definition {DefinitionId}", definitionId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetReleaseDefinition", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/releases")]
    public async Task<ActionResult> GetReleases(
        string projectName,
        [FromQuery] int? definitionId = null)
    {
        try
        {
            var releases = await devOpsService.GetReleasesAsync(projectName, definitionId);
            return Ok(new { success = true, releases = releases.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting releases for project {ProjectName}", projectName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetReleases", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/repositories/{repositoryName}/files")]
    public async Task<ActionResult> GetRepositoryFileContent(
        string projectName,
        string repositoryName,
        [FromQuery] string filePath,
        [FromQuery] string? branch = null)
    {
        try
        {
            var content = await devOpsService.GetRepositoryFileContentAsync(projectName, repositoryName, filePath, branch);
            if (content is null)
                return NotFound(new { success = false, error = $"File {filePath} not found" });

            return Ok(new { success = true, content });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting file content for {FilePath}", filePath);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetRepositoryFileContent", type = ex.GetType().Name });
        }
    }

    [HttpPut("{projectName}/repositories/{repositoryName}/files")]
    public async Task<ActionResult> UpdateRepositoryFile(
        string projectName,
        string repositoryName,
        [FromBody] UpdateFileRequest request)
    {
        try
        {
            var success = await devOpsService.UpdateRepositoryFileAsync(
                projectName, 
                repositoryName, 
                request.FilePath, 
                request.Content, 
                request.CommitMessage, 
                request.Branch);
            return Ok(new { success });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating file {FilePath}", request.FilePath);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "UpdateRepositoryFile", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/repositories/{repositoryName}/yaml-pipelines")]
    public async Task<ActionResult> FindYamlPipelineFiles(string projectName, string repositoryName)
    {
        try
        {
            var files = await devOpsService.FindYamlPipelineFilesAsync(projectName, repositoryName);
            return Ok(new { success = true, files = files.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding YAML pipeline files in repository {RepositoryName}", repositoryName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "FindYamlPipelineFiles", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/build-definitions/{definitionId}/yaml")]
    public async Task<ActionResult> GetPipelineYaml(string projectName, int definitionId)
    {
        try
        {
            var yaml = await devOpsService.GetPipelineYamlAsync(projectName, definitionId);
            if (yaml is null)
                return NotFound(new { success = false, error = $"YAML for definition {definitionId} not found" });

            return Ok(new { success = true, yaml });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting pipeline YAML for definition {DefinitionId}", definitionId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetPipelineYaml", type = ex.GetType().Name });
        }
    }

    [HttpPut("{projectName}/build-definitions/{definitionId}/yaml")]
    public async Task<ActionResult> UpdatePipelineYaml(
        string projectName,
        int definitionId,
        [FromBody] UpdatePipelineYamlRequest request)
    {
        try
        {
            var success = await devOpsService.UpdatePipelineYamlAsync(
                projectName, 
                definitionId, 
                request.YamlContent, 
                request.CommitMessage);
            return Ok(new { success });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating pipeline YAML for definition {DefinitionId}", definitionId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "UpdatePipelineYaml", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds/{buildId}/logs")]
    public async Task<ActionResult> GetBuildLogs(string projectName, int buildId)
    {
        try
        {
            var logs = await devOpsService.GetBuildLogsAsync(projectName, buildId);
            return Ok(new { success = true, logs = logs.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build logs for build {BuildId}", buildId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildLogs", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds/{buildId}/logs/{logId}")]
    public async Task<ActionResult> GetBuildLogContent(string projectName, int buildId, int logId)
    {
        try
        {
            var logContent = await devOpsService.GetBuildLogContentAsync(projectName, buildId, logId);
            if (logContent is null)
                return NotFound(new { success = false, error = $"Log {logId} not found" });

            return Ok(new { success = true, logContent });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build log content for log {LogId}", logId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildLogContent", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds/{buildId}/timeline")]
    public async Task<ActionResult> GetBuildTimeline(string projectName, int buildId)
    {
        try
        {
            var timeline = await devOpsService.GetBuildTimelineAsync(projectName, buildId);
            if (timeline is null)
                return NotFound(new { success = false, error = $"Timeline for build {buildId} not found" });

            return Ok(new { success = true, timeline });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build timeline for build {BuildId}", buildId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildTimeline", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds/{buildId}/step-logs")]
    public async Task<ActionResult> GetBuildStepLogs(string projectName, int buildId)
    {
        try
        {
            var stepLogs = await devOpsService.GetBuildStepLogsAsync(projectName, buildId);
            return Ok(new { success = true, stepLogs = stepLogs.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build step logs for build {BuildId}", buildId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildStepLogs", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds/{buildId}/complete-log")]
    public async Task<ActionResult> GetCompleteBuildLog(string projectName, int buildId)
    {
        try
        {
            var log = await devOpsService.GetCompleteBuildLogAsync(projectName, buildId);
            return Ok(new { success = true, log });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting complete build log for build {BuildId}", buildId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetCompleteBuildLog", type = ex.GetType().Name });
        }
    }

    [HttpGet("{projectName}/builds/{buildId}/tasks/{taskId}/log")]
    public async Task<ActionResult> GetBuildTaskLog(string projectName, int buildId, string taskId)
    {
        try
        {
            var taskLog = await devOpsService.GetBuildTaskLogAsync(projectName, buildId, taskId);
            if (taskLog is null)
                return NotFound(new { success = false, error = $"Task log for task {taskId} not found" });

            return Ok(new { success = true, taskLog });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting build task log for task {TaskId}", taskId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetBuildTaskLog", type = ex.GetType().Name });
        }
    }

    [HttpPost("{projectName}/builds/{buildId}/logs/search")]
    public async Task<ActionResult> SearchBuildLogsWithRegex(
        string projectName,
        int buildId,
        [FromBody] DevOpsSearchLogsRequest request)
    {
        try
        {
            var result = await devOpsService.SearchBuildLogsWithRegexAsync(
                projectName, 
                buildId, 
                request.RegexPattern,
                request.ContextLines ?? 3,
                request.CaseSensitive ?? false,
                request.MaxMatches ?? 50);
            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching build logs for build {BuildId}", buildId);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "SearchBuildLogsWithRegex", type = ex.GetType().Name });
        }
    }
}

public record CreateWorkItemRequest(string WorkItemType, string Title, Dictionary<string, object>? Fields = null);
public record QueueBuildRequest(int DefinitionId, string? Branch = null);
public record UpdateFileRequest(string FilePath, string Content, string CommitMessage, string? Branch = null);
public record UpdatePipelineYamlRequest(string YamlContent, string CommitMessage);
public record DevOpsSearchLogsRequest(string RegexPattern, int? ContextLines = 3, bool? CaseSensitive = false, int? MaxMatches = 50);