namespace PdfMcp.Models;

public class LoadPdfResult
{
    public string Message { get; set; } = "";
    public PdfMetadata? Metadata { get; set; }
    public int PageCount { get; set; }
}