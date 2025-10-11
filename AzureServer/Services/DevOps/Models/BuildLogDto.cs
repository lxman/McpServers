namespace AzureServer.Services.DevOps.Models;

public class BuildLogDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime? CreatedOn { get; set; }
    public DateTime? LastChangedOn { get; set; }
    public long LineCount { get; set; }
}