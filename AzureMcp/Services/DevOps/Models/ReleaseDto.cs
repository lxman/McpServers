namespace AzureMcp.Services.DevOps.Models;

public class ReleaseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public ReleaseDefinitionDto? ReleaseDefinition { get; set; }
    public string Url { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
}