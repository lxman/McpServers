namespace PdfMcp.Models;

public class DocumentInfo
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public int PageCount { get; set; }
    public string? Title { get; set; }
    public string? Author { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsEncrypted { get; set; }
}