namespace AwsServer.Core.Models.Controllers.Models;

public class LogGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Arn { get; set; }
    public long StoredBytes { get; set; }
    public int? RetentionInDays { get; set; }
    public DateTime CreationTime { get; set; }
}