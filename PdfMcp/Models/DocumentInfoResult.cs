namespace PdfMcp.Models;

public class DocumentInfoResult
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public PdfMetadata Metadata { get; set; } = new();
    public int PageCount { get; set; }
    public bool HasImages { get; set; }
    public bool HasAnnotations { get; set; }
    public bool HasLinks { get; set; }
}