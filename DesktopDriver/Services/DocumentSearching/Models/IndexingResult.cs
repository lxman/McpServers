namespace DesktopDriver.Services.DocumentSearching.Models;

public class IndexingResult
{
    public string IndexName { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public List<string> Successful { get; set; } = [];
    public List<FailedDocument> Failed { get; set; } = [];
    public List<PasswordProtectedDocument> PasswordProtected { get; set; } = [];
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public Dictionary<string, int> FileTypeStats { get; set; } = new();
    public long TotalSizeBytes { get; set; }
}
