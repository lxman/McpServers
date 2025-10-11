namespace PdfMcp.Models;

public class PdfMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public DateTime ModificationDate { get; set; }
    public string Version { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public bool IsEncrypted { get; set; }
    public bool HasDigitalSignatures { get; set; }
    public string SecuritySettings { get; set; } = string.Empty;
}