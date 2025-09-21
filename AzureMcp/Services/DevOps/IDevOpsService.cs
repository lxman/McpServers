using AzureMcp.Services.DevOps.Models;

namespace AzureMcp.Services.DevOps;

public interface IDevOpsService
{
    Task<IEnumerable<ProjectDto>> GetProjectsAsync();
    Task<ProjectDto?> GetProjectAsync(string projectName);
    Task<WorkItemDto?> GetWorkItemAsync(int id);
    Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string projectName, string? wiql = null);
    Task<WorkItemDto> CreateWorkItemAsync(string projectName, string workItemType, string title, Dictionary<string, object>? fields = null);
    Task<IEnumerable<RepositoryDto>> GetRepositoriesAsync(string projectName);
    Task<RepositoryDto?> GetRepositoryAsync(string projectName, string repositoryName);
}
