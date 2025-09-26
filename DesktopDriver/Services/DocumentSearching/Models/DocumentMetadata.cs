namespace DesktopDriver.Services.DocumentSearching.Models;

public class DocumentMetadata
{
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public DateTime AccessedDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}