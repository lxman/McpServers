using System.Text.RegularExpressions;
using AzureMcp.Authentication;
using AzureMcp.Services.DevOps.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;

namespace AzureMcp.Services.DevOps;

public class DevOpsService : IDevOpsService
{
    private readonly DevOpsCredentialManager _credentialManager;
    private readonly ILogger<DevOpsService> _logger;

    public DevOpsService(DevOpsCredentialManager credentialManager, ILogger<DevOpsService> logger)
    {
        _credentialManager = credentialManager;
        _logger = logger;
    }

    public async Task<IEnumerable<ProjectDto>> GetProjectsAsync()
    {
        try
        {
            var projectClient = _credentialManager.GetClient<ProjectHttpClient>();
            IPagedList<TeamProjectReference>? projects = await projectClient.GetProjects();

            return projects.Select(MapToProjectDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects");
            throw;
        }
    }

    public async Task<ProjectDto?> GetProjectAsync(string projectName)
    {
        try
        {
            var projectClient = _credentialManager.GetClient<ProjectHttpClient>();
            var project = await projectClient.GetProject(projectName);

            return project != null ? MapToProjectDto(project) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<WorkItemDto?> GetWorkItemAsync(int id)
    {
        try
        {
            var workItemClient = _credentialManager.GetClient<WorkItemTrackingHttpClient>();
            var workItem = await workItemClient.GetWorkItemAsync(id, expand: WorkItemExpand.All);

            return workItem != null ? MapToWorkItemDto(workItem) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item {WorkItemId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string projectName, string? wiql = null)
    {
        try
        {
            var workItemClient = _credentialManager.GetClient<WorkItemTrackingHttpClient>();
            
            // Default WIQL query if none provided
            wiql ??= $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{projectName}' ORDER BY [System.Id] DESC";
            
            var query = new Wiql { Query = wiql };
            var result = await workItemClient.QueryByWiqlAsync(query);

            if (result?.WorkItems == null || !result.WorkItems.Any())
                return [];

            var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
            List<WorkItem>? workItems = await workItemClient.GetWorkItemsAsync(ids, expand: WorkItemExpand.All);

            return workItems.Select(MapToWorkItemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items for project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<WorkItemDto> CreateWorkItemAsync(string projectName, string workItemType, string title, Dictionary<string, object>? fields = null)
    {
        try
        {
            var workItemClient = _credentialManager.GetClient<WorkItemTrackingHttpClient>();

            var patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Title",
                    Value = title
                }
            };

            // Add additional fields if provided
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    patchDocument.Add(new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{field.Key}",
                        Value = field.Value
                    });
                }
            }

            var workItem = await workItemClient.CreateWorkItemAsync(patchDocument, projectName, workItemType);
            return MapToWorkItemDto(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work item in project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<IEnumerable<RepositoryDto>> GetRepositoriesAsync(string projectName)
    {
        try
        {
            var gitClient = _credentialManager.GetClient<GitHttpClient>();
            List<GitRepository>? repositories = await gitClient.GetRepositoriesAsync(projectName);

            return repositories.Select(MapToRepositoryDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving repositories for project {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<RepositoryDto?> GetRepositoryAsync(string projectName, string repositoryName)
    {
        try
        {
            var gitClient = _credentialManager.GetClient<GitHttpClient>();
            var repository = await gitClient.GetRepositoryAsync(projectName, repositoryName);

            return repository != null ? MapToRepositoryDto(repository) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving repository {RepositoryName} in project {ProjectName}", repositoryName, projectName);
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
        return fields.TryGetValue(fieldName, out var value) ? value?.ToString() : null;
    }

    private static DateTime? GetDateTimeValue(IDictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value))
        {
            if (value is DateTime dateTime)
                return dateTime;
            if (DateTime.TryParse(value?.ToString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private static int? GetIntValue(IDictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value))
        {
            if (value is int intValue)
                return intValue;
            if (int.TryParse(value?.ToString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private static string ExtractDisplayName(string? userField)
    {
        if (string.IsNullOrEmpty(userField))
            return string.Empty;

        // Azure DevOps user fields often come in format "Display Name <email@domain.com>"
        var match = Regex.Match(userField, @"^([^<]+)");
        return match.Success ? match.Groups[1].Value.Trim() : userField;
    }
}
