namespace DesktopCommander.Services.DocumentSearching.Models;

public class PasswordProtectedDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string DetectedPattern { get; set; } = string.Empty;
    public bool PasswordAttempted { get; set; }
}