namespace DesktopCommander.Services.DocumentSearching.Models;

public class DocumentContent
{
    public string FilePath { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public Dictionary<string, object> StructuredData { get; set; } = new();
    public List<DocumentSection> Sections { get; set; } = [];
    public DocumentMetadata Metadata { get; set; } = new();
    public List<string> ExtractedLinks { get; set; } = [];
    public List<string> KeyTerms { get; set; } = [];
    public bool RequiredPassword { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}