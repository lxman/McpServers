using Microsoft.TeamFoundation.Build.WebApi;

namespace AzureMcp.Services.DevOps.Models;

public class BuildTimelineDto
{
    public string Id { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? FinishTime { get; set; }
    public int PercentComplete { get; set; }
    public string State { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public int? ResultCode { get; set; }
    public int Order { get; set; }
    public BuildLogDto? Log { get; set; }
    public List<BuildTimelineDto> Children { get; set; } = [];
    public List<Issue> Issues { get; set; } = [];
}