using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AzureMcp.Authentication;
using AzureMcp.Common;
using AzureMcp.Services.DevOps.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Clients;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AzureMcp.Services.DevOps;

public class DevOpsService(DevOpsCredentialManager credentialManager, ILogger<DevOpsService> logger)
    : IDevOpsService
{
    public async Task<IEnumerable<ProjectDto>> GetProjectsAsync()
    {
        try
        {
            var projectClient = credentialManager.GetClient<ProjectHttpClient>();
            IPagedList<TeamProjectReference>? projects = await projectClient.GetProjects();

            return projects.Select(MapToProjectDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving projects");
            throw;
        }
    }

    public async Task<ProjectDto?> GetProjectAsync(string projectName)
    {
        try
        {
            var projectClient = credentialManager.GetClient<ProjectHttpClient>();
            TeamProject? project = await projectClient.GetProject(projectName);

            return project != null ? MapToProjectDto(project) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<WorkItemDto?> GetWorkItemAsync(int id)
    {
        try
        {
            var workItemClient = credentialManager.GetClient<WorkItemTrackingHttpClient>();
            WorkItem? workItem = await workItemClient.GetWorkItemAsync(id, expand: WorkItemExpand.All);

            return workItem != null ? MapToWorkItemDto(workItem) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving work item {WorkItemId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string projectName, string? wiql = null)
    {
        try
        {
            var workItemClient = credentialManager.GetClient<WorkItemTrackingHttpClient>();
            
            // Default WIQL query if none provided
            wiql ??= $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{projectName}' ORDER BY [System.Id] DESC";
            
            var query = new Wiql { Query = wiql };
            WorkItemQueryResult? result = await workItemClient.QueryByWiqlAsync(query);

            if (result?.WorkItems == null || !result.WorkItems.Any())
                return [];

            int[] ids = result.WorkItems.Select(wi => wi.Id).ToArray();
            List<WorkItem>? workItems = await workItemClient.GetWorkItemsAsync(ids, expand: WorkItemExpand.All);

            return workItems.Select(MapToWorkItemDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving work items for project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<WorkItemDto> CreateWorkItemAsync(string projectName, string workItemType, string title, Dictionary<string, object>? fields = null)
    {
        try
        {
            var workItemClient = credentialManager.GetClient<WorkItemTrackingHttpClient>();

            var patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Title",
                    Value = title
                }
            };

            // Add additional fields if provided
            if (fields != null)
            {
                foreach (KeyValuePair<string, object> field in fields)
                {
                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{field.Key}",
                        Value = field.Value
                    });
                }
            }

            WorkItem? workItem = await workItemClient.CreateWorkItemAsync(patchDocument, projectName, workItemType);
            return MapToWorkItemDto(workItem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating work item in project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<IEnumerable<RepositoryDto>> GetRepositoriesAsync(string projectName)
    {
        try
        {
            var gitClient = credentialManager.GetClient<GitHttpClient>();
            List<GitRepository>? repositories = await gitClient.GetRepositoriesAsync(projectName);

            return repositories.Select(MapToRepositoryDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving repositories for project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<RepositoryDto?> GetRepositoryAsync(string projectName, string repositoryName)
    {
        try
        {
            var gitClient = credentialManager.GetClient<GitHttpClient>();
            GitRepository? repository = await gitClient.GetRepositoryAsync(projectName, repositoryName);

            return repository != null ? MapToRepositoryDto(repository) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving repository {RepositoryName} in project {ProjectName}", repositoryName, projectName);
            throw;
        }
    }

    private static ProjectDto MapToProjectDto(TeamProjectReference project)
    {
        return new ProjectDto
        {
            Id = project.Id.ToString(),
            Name = project.Name,
            Description = project.Description ?? string.Empty,
            State = project.State.ToString(),
            Url = project.Url,
            Visibility = project.Visibility.ToString()
        };
    }

    private static WorkItemDto MapToWorkItemDto(WorkItem workItem)
    {
        IDictionary<string, object>? fields = workItem.Fields;
        
        return new WorkItemDto
        {
            Id = workItem.Id ?? 0,
            Title = GetFieldValue(fields, "System.Title") ?? string.Empty,
            WorkItemType = GetFieldValue(fields, "System.WorkItemType") ?? string.Empty,
            State = GetFieldValue(fields, "System.State") ?? string.Empty,
            AssignedTo = ExtractDisplayName(GetFieldValue(fields, "System.AssignedTo")),
            CreatedBy = ExtractDisplayName(GetFieldValue(fields, "System.CreatedBy")),
            CreatedDate = GetDateTimeValue(fields, "System.CreatedDate"),
            ChangedDate = GetDateTimeValue(fields, "System.ChangedDate"),
            Description = GetFieldValue(fields, "System.Description"),
            AcceptanceCriteria = GetFieldValue(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"),
            Priority = GetIntValue(fields, "Microsoft.VSTS.Common.Priority"),
            Tags = GetFieldValue(fields, "System.Tags"),
            ProjectName = GetFieldValue(fields, "System.TeamProject") ?? string.Empty,
            Url = workItem.Url ?? string.Empty
        };
    }

    private static RepositoryDto MapToRepositoryDto(GitRepository repository)
    {
        return new RepositoryDto
        {
            Id = repository.Id.ToString(),
            Name = repository.Name,
            DefaultBranch = repository.DefaultBranch ?? string.Empty,
            ProjectName = repository.ProjectReference?.Name ?? string.Empty,
            ProjectId = repository.ProjectReference?.Id.ToString() ?? string.Empty,
            Url = repository.Url,
            WebUrl = repository.WebUrl ?? string.Empty,
            CloneUrl = repository.RemoteUrl ?? string.Empty,
            Size = repository.Size ?? 0,
            IsDisabled = repository.IsDisabled ?? false
        };
    }

    private static string? GetFieldValue(IDictionary<string, object> fields, string fieldName)
    {
        return fields.TryGetValue(fieldName, out object? value) ? value?.ToString() : null;
    }

    private static DateTime? GetDateTimeValue(IDictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out object? value))
        {
            if (value is DateTime dateTime)
                return dateTime;
            if (DateTime.TryParse(value?.ToString(), out DateTime parsed))
                return parsed;
        }
        return null;
    }

    private static int? GetIntValue(IDictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out object? value))
        {
            if (value is int intValue)
                return intValue;
            if (int.TryParse(value?.ToString(), out int parsed))
                return parsed;
        }
        return null;
    }

    private static string ExtractDisplayName(string? userField)
    {
        if (string.IsNullOrEmpty(userField))
            return string.Empty;

        // Azure DevOps user fields often come in format "Display Name <email@domain.com>"
        Match match = Regex.Match(userField, @"^([^<]+)");
        return match.Success ? match.Groups[1].Value.Trim() : userField;
    }
    
    #region Pipeline Methods

    public async Task<IEnumerable<BuildDefinitionDto>> GetBuildDefinitionsAsync(string projectName)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            List<BuildDefinitionReference>? definitions = await buildClient.GetDefinitionsAsync(projectName);
            var fullDefinitions = new List<BuildDefinition>();
            foreach (BuildDefinitionReference defRef in definitions)
            {
                BuildDefinition? fullDef = await buildClient.GetDefinitionAsync(projectName, defRef.Id);
                if (fullDef != null) fullDefinitions.Add(fullDef);
            }
            return fullDefinitions.Select(MapToBuildDefinitionDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving build definitions for project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<BuildDefinitionDto?> GetBuildDefinitionAsync(string projectName, int definitionId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            BuildDefinition? definition = await buildClient.GetDefinitionAsync(projectName, definitionId);
            
            return definition != null ? MapToBuildDefinitionDto(definition) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving build definition {DefinitionId} from project {ProjectName}", definitionId, projectName);
            throw;
        }
    }

    public async Task<IEnumerable<BuildDto>> GetBuildsAsync(string projectName, int? definitionId = null, int? top = null)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            List<Build>? builds = await buildClient.GetBuildsAsync(
                projectName, 
                definitions: definitionId.HasValue ? new[] { definitionId.Value } : null,
                top: top);
            
            return builds.Select(MapToBuildDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving builds for project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<BuildDto?> GetBuildAsync(string projectName, int buildId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            Build? build = await buildClient.GetBuildAsync(projectName, buildId);
            
            return build != null ? MapToBuildDto(build) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving build {BuildId} from project {ProjectName}", buildId, projectName);
            throw;
        }
    }

    public async Task<BuildDto> QueueBuildAsync(string projectName, int definitionId, string? branch = null)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            
            var buildRequest = new Build
            {
                Definition = new DefinitionReference { Id = definitionId },
                Project = new TeamProjectReference { Name = projectName }
            };
            
            if (!string.IsNullOrEmpty(branch))
            {
                buildRequest.SourceBranch = branch;
            }
            
            Build? queuedBuild = await buildClient.QueueBuildAsync(buildRequest, projectName);
            return MapToBuildDto(queuedBuild);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queuing build {DefinitionId} for project {ProjectName}", definitionId, projectName);
            throw;
        }
    }

    public async Task<IEnumerable<ReleaseDefinitionDto>> GetReleaseDefinitionsAsync(string projectName)
    {
        try
        {
            var releaseClient = credentialManager.GetClient<ReleaseHttpClient>();
            List<ReleaseDefinition>? definitions = await releaseClient.GetReleaseDefinitionsAsync(projectName);
            
            return definitions.Select(MapToReleaseDefinitionDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving release definitions for project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<ReleaseDefinitionDto?> GetReleaseDefinitionAsync(string projectName, int definitionId)
    {
        try
        {
            var releaseClient = credentialManager.GetClient<ReleaseHttpClient>();
            ReleaseDefinition? definition = await releaseClient.GetReleaseDefinitionAsync(projectName, definitionId);
            
            return definition != null ? MapToReleaseDefinitionDto(definition) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving release definition {DefinitionId} from project {ProjectName}", definitionId, projectName);
            throw;
        }
    }

    public async Task<IEnumerable<ReleaseDto>> GetReleasesAsync(string projectName, int? definitionId = null)
    {
        try
        {
            var releaseClient = credentialManager.GetClient<ReleaseHttpClient>();
            List<Release>? releases = await releaseClient.GetReleasesAsync(
                projectName, 
                definitionId: definitionId);
            
            return releases.Select(MapToReleaseDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving releases for project {ProjectName}", projectName);
            throw;
        }
    }

    #endregion

    #region YAML File Methods

    public async Task<string?> GetRepositoryFileContentAsync(string projectName, string repositoryName, string filePath, string? branch = null)
    {
        try
        {
            var gitClient = credentialManager.GetClient<GitHttpClient>();
            
            GitItem? item = await gitClient.GetItemAsync(
                project: projectName,
                repositoryId: repositoryName,
                filePath,
                versionDescriptor: branch != null ? new GitVersionDescriptor 
                { 
                    Version = branch, 
                    VersionType = GitVersionType.Branch 
                } : null,
                includeContent: true);
            
            return item?.Content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving file {FilePath} from repository {RepositoryName} in project {ProjectName}", 
                filePath, repositoryName, projectName);
            throw;
        }
    }

    public async Task<bool> UpdateRepositoryFileAsync(string projectName, string repositoryName, string filePath, 
        string content, string commitMessage, string? branch = null)
    {
        try
        {
            var gitClient = credentialManager.GetClient<GitHttpClient>();
            
            // Get current item to get object ID for update
            GitItem? currentItem = null;
            try
            {
                currentItem = await gitClient.GetItemAsync(
                    repositoryId: repositoryName,
                    path: filePath,
                    project: projectName,
                    versionDescriptor: branch != null ? new GitVersionDescriptor 
                    { 
                        Version = branch, 
                        VersionType = GitVersionType.Branch 
                    } : null,
                    includeContent: true);
            }
            catch
            {
                // File doesn't exist - will be created
            }

            string targetBranch = branch ?? "refs/heads/main";
            VersionControlChangeType changeType = currentItem != null ? VersionControlChangeType.Edit : VersionControlChangeType.Add;
            
            var commit = new GitCommitRef
            {
                Comment = commitMessage,
                Changes =
                [
                    new GitChange
                    {
                        ChangeType = changeType,
                        Item = new GitItem { Path = filePath },
                        NewContent = new ItemContent
                        {
                            Content = content,
                            ContentType = ItemContentType.RawText
                        }
                    }
                ]
            };

            var push = new GitPush
            {
                RefUpdates =
                [
                    new GitRefUpdate
                    {
                        Name = targetBranch,
                        OldObjectId = currentItem?.ObjectId ?? "0000000000000000000000000000000000000000"
                    }
                ],
                Commits = [commit]
            };

            await gitClient.CreatePushAsync(push, repositoryName, projectName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating file {FilePath} in repository {RepositoryName}", filePath, repositoryName);
            return false;
        }
    }

    public async Task<IEnumerable<string>> FindYamlPipelineFilesAsync(string projectName, string repositoryName)
    {
        try
        {
            var gitClient = credentialManager.GetClient<GitHttpClient>();
            
            // Search for YAML files in common pipeline locations
            var searchPaths = new[] { "/", "/.azure-pipelines", "/.github/workflows", "/pipelines", "/build" };
            var yamlFiles = new List<string>();
            
            foreach (string searchPath in searchPaths)
            {
                try
                {
                    List<GitItem>? items = await gitClient.GetItemsAsync(
                        repositoryName,
                        projectName,
                        scopePath: searchPath,
                        recursionLevel: VersionControlRecursionType.Full);
                    
                    IEnumerable<string> yamlItems = items.Where(item => 
                        item.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                        item.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Path);
                    
                    yamlFiles.AddRange(yamlItems);
                }
                catch
                {
                    // Path might not exist, continue
                }
            }
            
            return yamlFiles.Distinct();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding YAML files in repository {RepositoryName}", repositoryName);
            throw;
        }
    }

    public async Task<string?> GetPipelineYamlAsync(string projectName, int definitionId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            BuildDefinition? definition = await buildClient.GetDefinitionAsync(projectName, definitionId);
            
            if (definition?.Process is YamlProcess yamlProcess)
            {
                var gitClient = credentialManager.GetClient<GitHttpClient>();
                GitItem? yamlContent = await gitClient.GetItemAsync(
                    project: projectName,
                    definition.Repository.Id,
                    path: yamlProcess.YamlFilename,
                    includeContent: true);
                
                return yamlContent?.Content;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving YAML for pipeline {DefinitionId}", definitionId);
            throw;
        }
    }

    public async Task<bool> UpdatePipelineYamlAsync(string projectName, int definitionId, string yamlContent, string commitMessage)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            BuildDefinition? definition = await buildClient.GetDefinitionAsync(projectName, definitionId);
            
            if (definition?.Process is YamlProcess yamlProcess && definition.Repository != null)
            {
                return await UpdateRepositoryFileAsync(
                    projectName,
                    definition.Repository.Name,
                    yamlProcess.YamlFilename,
                    yamlContent,
                    commitMessage);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating YAML for pipeline {DefinitionId}", definitionId);
            return false;
        }
    }

    #endregion

    #region Mapping Methods

    private static BuildDefinitionDto MapToBuildDefinitionDto(BuildDefinition definition)
    {
        return new BuildDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Path = definition.Path ?? "/",
            Type = definition.Type.ToString(),
            QueueStatus = definition.QueueStatus.ToString(),
            Description = definition.Description,
            Repository = definition.Repository != null ? new RepositoryDto
            {
                Id = definition.Repository.Id,
                Name = definition.Repository.Name ?? "",
                DefaultBranch = definition.Repository.DefaultBranch ?? "",
                ProjectName = definition.Project?.Name ?? "",
                Url = definition.Repository.Url?.ToString() ?? "",
                WebUrl = definition.Repository.Url?.ToString() ?? ""
            } : null,
            YamlFilename = (definition.Process as YamlProcess)?.YamlFilename,
            CreatedDate = definition.CreatedDate,
            Url = definition.Url ?? "",
            WebUrl = definition.Uri?.ToString() ?? ""
        };
    }

    private static BuildDto MapToBuildDto(Build build)
    {
        return new BuildDto
        {
            Id = build.Id,
            BuildNumber = build.BuildNumber,
            Status = build.Status?.ToString() ?? "",
            Result = build.Result?.ToString() ?? "",
            StartTime = build.StartTime,
            FinishTime = build.FinishTime,
            RequestedFor = build.RequestedFor?.DisplayName,
            RequestedBy = build.RequestedBy?.DisplayName,
            Definition = build.Definition != null ? new BuildDefinitionDto
            {
                Id = build.Definition.Id,
                Name = build.Definition.Name,
                Path = build.Definition.Path ?? "/"
            } : null,
            SourceBranch = build.SourceBranch,
            SourceVersion = build.SourceVersion,
            Url = build.Url ?? "",
            WebUrl = build.Uri?.ToString() ?? ""
        };
    }

    private static ReleaseDefinitionDto MapToReleaseDefinitionDto(ReleaseDefinition definition)
    {
        return new ReleaseDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Path = definition.Path ?? "/",
            Description = definition.Description,
            CreatedOn = definition.CreatedOn,
            CreatedBy = definition.CreatedBy?.DisplayName,
            Url = definition.Url ?? "",
            Environments = definition.Environments?.Select(env => new EnvironmentDto
            {
                Id = env.Id,
                Name = env.Name,
                Rank = env.Rank
            }).ToList() ?? []
        };
    }

    private static ReleaseDto MapToReleaseDto(Release release)
    {
        return new ReleaseDto
        {
            Id = release.Id,
            Name = release.Name,
            Status = release.Status.ToString(),
            CreatedOn = release.CreatedOn,
            CreatedBy = release.CreatedBy?.DisplayName,
            ReleaseDefinition = release.ReleaseDefinitionReference != null ? new ReleaseDefinitionDto
            {
                Id = release.ReleaseDefinitionReference.Id,
                Name = release.ReleaseDefinitionReference.Name,
                Path = release.ReleaseDefinitionReference.Path ?? "/"
            } : null,
            Url = release.Links?.ToString() ?? ""
        };
    }

    #endregion
    
    #region Build Log Methods

    public async Task<IEnumerable<BuildLogDto>> GetBuildLogsAsync(string projectName, int buildId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            List<BuildLog>? logs = await buildClient.GetBuildLogsAsync(projectName, buildId);
            
            return logs.Select(log => new BuildLogDto
            {
                Id = log.Id,
                Type = log.Type ?? string.Empty,
                Url = log.Url ?? string.Empty,
                CreatedOn = log.CreatedOn,
                LastChangedOn = log.LastChangedOn,
                LineCount = log.LineCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving build logs for build {BuildId} in project {ProjectName}", buildId, projectName);
            throw;
        }
    }

    public async Task<BuildLogContentDto?> GetBuildLogContentAsync(string projectName, int buildId, int logId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            
            // Get the log content as a stream
            await using Stream? logStream = await buildClient.GetBuildLogAsync(projectName, buildId, logId);
            using var reader = new StreamReader(logStream);
            string content = await reader.ReadToEndAsync();
            
            List<BuildLog>? logs = await buildClient.GetBuildLogsAsync(projectName, buildId);
            BuildLog? logInfo = logs.FirstOrDefault(l => l.Id == logId);
            
            return new BuildLogContentDto
            {
                LogId = logId,
                Content = content,
                LineCount = logInfo?.LineCount ?? content.Split('\n').Length,
                IsTruncated = content.Length >= 100000
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving build log content for log {LogId} in build {BuildId}", logId, buildId);
            throw;
        }
    }

    public async Task<BuildTimelineDto?> GetBuildTimelineAsync(string projectName, int buildId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            Timeline? timeline = await buildClient.GetBuildTimelineAsync(projectName, buildId);
            
            if (timeline?.Records == null)
                return null;

            // Build a hierarchical structure from timeline records
            Dictionary<string, TimelineRecord> recordDict = timeline.Records.ToDictionary(r => r.Id.ToString(), r => r);
            List<TimelineRecord> rootRecords = timeline.Records.Where(r => string.IsNullOrEmpty(r.ParentId.ToString())).ToList();
            
            var result = new BuildTimelineDto
            {
                Id = timeline.Id.ToString(),
                Type = "Timeline",
                Name = "Build Timeline",
                Children = rootRecords.Select(r => MapTimelineRecord(r, recordDict)).ToList()
            };
            
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving build timeline for build {BuildId} in project {ProjectName}", buildId, projectName);
            throw;
        }
    }

    public async Task<IEnumerable<BuildStepLogDto>> GetBuildStepLogsAsync(string projectName, int buildId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            Timeline? timeline = await buildClient.GetBuildTimelineAsync(projectName, buildId);
            
            if (timeline?.Records == null)
                return [];

            var stepLogs = new List<BuildStepLogDto>();
            
            foreach (TimelineRecord? record in timeline.Records.Where(r => r.Log != null))
            {
                try
                {
                    await using Stream? logStream = await buildClient.GetBuildLogAsync(projectName, buildId, record.Log.Id);
                    using var reader = new StreamReader(logStream);
                    string logContent = await reader.ReadToEndAsync();
                    
                    var stepLog = new BuildStepLogDto
                    {
                        StepName = record.Name ?? "Unknown Step",
                        StepId = record.Id.ToString(),
                        StartTime = record.StartTime,
                        FinishTime = record.FinishTime,
                        Status = record.State?.ToString() ?? string.Empty,
                        Result = record.Result?.ToString() ?? string.Empty,
                        LogContent = logContent,
                        ErrorMessages = ExtractErrorMessages(logContent),
                        WarningMessages = ExtractWarningMessages(logContent)
                    };
                    
                    stepLogs.Add(stepLog);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not retrieve log for step {StepName}", record.Name);
                }
            }
            
            return stepLogs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving build step logs for build {BuildId}", buildId);
            throw;
        }
    }

    public async Task<string> GetCompleteBuildLogAsync(string projectName, int buildId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            List<BuildLog>? logs = await buildClient.GetBuildLogsAsync(projectName, buildId);
            
            var combinedLog = new StringBuilder();
            combinedLog.AppendLine($"=== COMPLETE BUILD LOG FOR BUILD {buildId} ===");
            combinedLog.AppendLine($"Project: {projectName}");
            combinedLog.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            combinedLog.AppendLine();
            
            foreach (BuildLog log in logs.OrderBy(l => l.Id))
            {
                try
                {
                    combinedLog.AppendLine($"=== LOG {log.Id}: {log.Type} ===");

                    await using Stream? logStream = await buildClient.GetBuildLogAsync(projectName, buildId, log.Id);
                    using var reader = new StreamReader(logStream);
                    string content = await reader.ReadToEndAsync();
                    
                    combinedLog.AppendLine(content);
                    combinedLog.AppendLine();
                }
                catch (Exception ex)
                {
                    combinedLog.AppendLine($"ERROR: Could not retrieve log {log.Id}: {ex.Message}");
                    combinedLog.AppendLine();
                }
            }
            
            return combinedLog.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving complete build log for build {BuildId}", buildId);
            throw;
        }
    }

    public async Task<BuildLogContentDto?> GetBuildTaskLogAsync(string projectName, int buildId, string taskId)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            Timeline? timeline = await buildClient.GetBuildTimelineAsync(projectName, buildId);
        
            TimelineRecord? taskRecord = timeline?.Records?.FirstOrDefault(r => r.Id.ToString() == taskId);
            if (taskRecord?.Log == null)
                return null;
            
            // Get the actual content
            await using Stream? logStream = await buildClient.GetBuildLogAsync(projectName, buildId, taskRecord.Log.Id);
            using var reader = new StreamReader(logStream);
            string content = await reader.ReadToEndAsync();
        
            // Get full log metadata (BuildLog has LineCount)
            List<BuildLog>? logs = await buildClient.GetBuildLogsAsync(projectName, buildId);
            BuildLog? fullLogInfo = logs.FirstOrDefault(l => l.Id == taskRecord.Log.Id);
        
            return new BuildLogContentDto
            {
                LogId = taskRecord.Log.Id,
                Content = content,
                LineCount = fullLogInfo?.LineCount ?? content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
                IsTruncated = content.Length >= 100000
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving task log for task {TaskId} in build {BuildId}", taskId, buildId);
            throw;
        }
    }

    public async Task<string> SearchBuildLogsWithRegexAsync(
        string projectName, int buildId, string regexPattern, 
        int contextLines = 3, bool caseSensitive = false, int maxMatches = 50)
    {
        try
        {
            var buildClient = credentialManager.GetClient<BuildHttpClient>();
            List<BuildLog>? logs = await buildClient.GetBuildLogsAsync(projectName, buildId);
            var matches = new List<object>();
            
            var regex = new Regex(regexPattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            
            foreach (BuildLog log in logs)
            {
                try
                {
                    await using Stream? logStream = await buildClient.GetBuildLogAsync(projectName, buildId, log.Id);
                    using var reader = new StreamReader(logStream);
                    string content = await reader.ReadToEndAsync();
                    string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            int contextStart = Math.Max(0, i - contextLines);
                            int contextEnd = Math.Min(lines.Length - 1, i + contextLines);
                            
                            matches.Add(new
                            {
                                LogId = log.Id,
                                LogType = log.Type,
                                LineNumber = i + 1,
                                MatchedLine = lines[i].Trim(),
                                Context = lines[contextStart..(contextEnd + 1)]
                                         .Select((line, idx) => new { 
                                             LineNum = contextStart + idx + 1, 
                                             Content = line.Trim(),
                                             IsMatch = contextStart + idx == i
                                         }).ToArray(),
                                Timestamp = ExtractTimestamp(lines[i])
                            });
                            
                            if (matches.Count >= maxMatches) break;
                        }
                    }
                    if (matches.Count >= maxMatches) break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not search log {LogId}", log.Id);
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                buildId,
                project = projectName,
                searchPattern = regexPattern,
                searchOptions = new { contextLines, caseSensitive, maxMatches },
                totalMatches = matches.Count,
                summary = GenerateSearchSummary(matches, regexPattern),
                matches = matches.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching build logs with regex for build {BuildId}", buildId);
            throw;
        }
    }

    #endregion

    #region Helper Methods for Build Logs

    private BuildTimelineDto MapTimelineRecord(TimelineRecord record, Dictionary<string, TimelineRecord> allRecords)
    {
        List<BuildTimelineDto> children = allRecords.Values
            .Where(r => r.ParentId == record.Id)
            .Select(r => MapTimelineRecord(r, allRecords))
            .ToList();
        
        return new BuildTimelineDto
        {
            Id = record.Id.ToString(),
            ParentId = record.ParentId.ToString() ?? string.Empty,
            Type = record.RecordType ?? string.Empty,
            Name = record.Name ?? string.Empty,
            StartTime = record.StartTime,
            FinishTime = record.FinishTime,
            PercentComplete = record.PercentComplete ?? 0,
            State = record.State?.ToString() ?? string.Empty,
            Result = record.Result?.ToString() ?? string.Empty,
            ResultCode = Convert.ToInt32(record.ResultCode),
            Order = record.Order ?? 0,
            Log = record.Log != null ? new BuildLogDto
            {
                Id = record.Log.Id,
                Type = record.Log.Type ?? string.Empty,
                Url = record.Log.Url ?? string.Empty,
                CreatedOn = DateTime.MinValue,
                LastChangedOn = DateTime.MinValue,
                LineCount = 0
            } : null,
            Children = children,
            Issues = record.Issues
        };
    }

    private static List<string> ExtractErrorMessages(string logContent)
    {
        var errors = new List<string>();
        string[] lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string line in lines)
        {
            if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("FAILED", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(line.Trim());
            }
        }
        
        return errors;
    }

    private static List<string> ExtractWarningMessages(string logContent)
    {
        var warnings = new List<string>();
        string[] lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string line in lines)
        {
            if (line.Contains("WARNING", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("WARN", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(line.Trim());
            }
        }
        
        return warnings;
    }
    
    private static string? ExtractTimestamp(string logLine)
    {
        // Extract Azure DevOps timestamp format: 2025-09-22T18:55:05.6745795Z
        Match timestampMatch = Regex.Match(logLine, @"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z)");
        return timestampMatch.Success ? timestampMatch.Groups[1].Value : null;
    }

    private static object GenerateSearchSummary(List<object> matches, string pattern)
    {
        if (!matches.Any()) return new { message = "No matches found" };
    
        // Analyze matches for common patterns
        List<dynamic> matchStrings = matches.Select(m => 
            ((dynamic)m).MatchedLine.ToString().ToLower()).ToList();
    
        var summary = new
        {
            totalMatches = matches.Count,
            commonPatterns = new
            {
                errorCount = matchStrings.Count(s => s.Contains("error")),
                warningCount = matchStrings.Count(s => s.Contains("warn")),
                kendoLicenseIssues = matchStrings.Count(s => s.Contains("tkl") || s.Contains("license")),
                npmIssues = matchStrings.Count(s => s.Contains("npm") && (s.Contains("warn") || s.Contains("error"))),
                dockerIssues = matchStrings.Count(s => s.Contains("#") && (s.Contains("error") || s.Contains("failed")))
            },
            timeRange = new
            {
                firstMatch = ((dynamic)matches.First()).Timestamp,
                lastMatch = ((dynamic)matches.Last()).Timestamp
            }
        };
    
        return summary;
    }

    #endregion
}
