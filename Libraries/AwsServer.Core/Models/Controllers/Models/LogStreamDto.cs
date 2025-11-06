namespace AwsServer.Core.Models.Controllers.Models;

public class LogStreamDto
{
    public string Name { get; set; } = string.Empty;
    public string? Arn { get; set; }
    public DateTime? FirstEventTime { get; set; }
    public DateTime? LastEventTime { get; set; }
    public DateTime CreationTime { get; set; }
    public long StoredBytes { get; set; }
}