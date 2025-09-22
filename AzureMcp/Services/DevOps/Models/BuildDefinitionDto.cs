namespace AzureMcp.Services.DevOps.Models;

public class BuildDefinitionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string QueueStatus { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RepositoryDto? Repository { get; set; }
    public string? YamlFilename { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string Url { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
}