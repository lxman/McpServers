namespace AzureServer.Core.Services.DevOps.Models;

public class WorkItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string WorkItemType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public DateTime? ChangedDate { get; set; }
    public string? Description { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public int? Priority { get; set; }
    public string? Tags { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
