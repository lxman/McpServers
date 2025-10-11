namespace PdfMcp.Models;

public class ExtractImagesResult
{
    public List<ExtractedImageInfo> ExtractedImages { get; set; } = [];
    public int TotalImages { get; set; }
    public string OutputDirectory { get; set; } = "";
    public string? Note { get; set; }
}