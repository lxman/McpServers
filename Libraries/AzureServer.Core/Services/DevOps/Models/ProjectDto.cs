namespace AzureServer.Core.Services.DevOps.Models;

public class ProjectDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedTime { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int WorkItemCount { get; set; }
    public int RepositoryCount { get; set; }
}
