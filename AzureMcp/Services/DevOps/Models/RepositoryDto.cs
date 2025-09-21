namespace AzureMcp.Services.DevOps.Models;

public class RepositoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime? CreatedDate { get; set; }
}
