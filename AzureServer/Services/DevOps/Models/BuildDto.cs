namespace AzureServer.Services.DevOps.Models;

public class BuildDto
{
    public int Id { get; set; }
    public string BuildNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? FinishTime { get; set; }
    public string? RequestedFor { get; set; }
    public string? RequestedBy { get; set; }
    public BuildDefinitionDto? Definition { get; set; }
    public string? SourceBranch { get; set; }
    public string? SourceVersion { get; set; }
    public string Url { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
}