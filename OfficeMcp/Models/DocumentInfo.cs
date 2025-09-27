namespace OfficeMcp.Models;

public class DocumentInfo
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DocumentType Type { get; set; }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public bool IsPasswordProtected { get; set; }
}