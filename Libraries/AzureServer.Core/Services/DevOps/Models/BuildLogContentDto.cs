namespace AzureServer.Core.Services.DevOps.Models;

public class BuildLogContentDto
{
    public int LogId { get; set; }
    public string Content { get; set; } = string.Empty;
    public long LineCount { get; set; }
    public bool IsTruncated { get; set; }
}