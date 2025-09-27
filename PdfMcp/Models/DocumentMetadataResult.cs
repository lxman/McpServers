namespace PdfMcp.Models;

public class DocumentMetadataResult
{
    public string FilePath { get; set; } = "";
    public PdfMetadata Metadata { get; set; } = new();
}