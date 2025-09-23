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
    Task<IEnumerable<BuildDefinitionDto>> GetBuildDefinitionsAsync(string projectName);
    Task<BuildDefinitionDto?> GetBuildDefinitionAsync(string projectName, int definitionId);
    Task<IEnumerable<BuildDto>> GetBuildsAsync(string projectName, int? definitionId = null, int? top = null);
    Task<BuildDto?> GetBuildAsync(string projectName, int buildId);
    Task<BuildDto> QueueBuildAsync(string projectName, int definitionId, string? branch = null);
    Task<IEnumerable<ReleaseDefinitionDto>> GetReleaseDefinitionsAsync(string projectName);
    Task<ReleaseDefinitionDto?> GetReleaseDefinitionAsync(string projectName, int definitionId);
    Task<IEnumerable<ReleaseDto>> GetReleasesAsync(string projectName, int? definitionId = null);
    Task<string?> GetRepositoryFileContentAsync(string projectName, string repositoryName, string filePath, string? branch = null);
    Task<bool> UpdateRepositoryFileAsync(string projectName, string repositoryName, string filePath, string content, string commitMessage, string? branch = null);
    Task<IEnumerable<string>> FindYamlPipelineFilesAsync(string projectName, string repositoryName);
    Task<string?> GetPipelineYamlAsync(string projectName, int definitionId);
    Task<bool> UpdatePipelineYamlAsync(string projectName, int definitionId, string yamlContent, string commitMessage);
    Task<IEnumerable<BuildLogDto>> GetBuildLogsAsync(string projectName, int buildId);
    Task<BuildLogContentDto?> GetBuildLogContentAsync(string projectName, int buildId, int logId);
    Task<BuildTimelineDto?> GetBuildTimelineAsync(string projectName, int buildId);
    Task<IEnumerable<BuildStepLogDto>> GetBuildStepLogsAsync(string projectName, int buildId);
    Task<string> GetCompleteBuildLogAsync(string projectName, int buildId);
    Task<BuildLogContentDto?> GetBuildTaskLogAsync(string projectName, int buildId, string taskId);
    Task<string> SearchBuildLogsWithRegexAsync(string projectName, int buildId, string regexPattern, 
        int contextLines = 3, bool caseSensitive = false, int maxMatches = 50);
}