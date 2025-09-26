namespace DesktopDriver.Services.DocumentSearching.Models;

public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public List<string> Snippets { get; set; } = [];
    public DocumentType DocumentType { get; set; }
    public DateTime ModifiedDate { get; set; }
    public long FileSizeBytes { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}