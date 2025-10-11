namespace PdfMcp.Models;

public class PdfDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public PdfMetadata Metadata { get; set; } = new();
    public List<PdfPage> Pages { get; set; } = [];
    public bool IsLoaded { get; set; }
    public string LoadError { get; set; } = string.Empty;
}