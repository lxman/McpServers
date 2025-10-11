namespace DesktopCommander.Services.DocumentSearching.Models;

public class FailedDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}