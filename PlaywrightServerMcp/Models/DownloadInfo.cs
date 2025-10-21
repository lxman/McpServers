namespace PlaywrightServerMcp.Models;

public class DownloadInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public long Size { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string Status { get; set; } = "pending"; // pending, completed, failed
    public string? Error { get; set; }
    public string TriggerSelector { get; set; } = "";
}