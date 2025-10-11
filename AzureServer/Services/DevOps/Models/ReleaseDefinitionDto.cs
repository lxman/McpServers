namespace AzureServer.Services.DevOps.Models;

public class ReleaseDefinitionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string Url { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
    public List<EnvironmentDto> Environments { get; set; } = [];
}