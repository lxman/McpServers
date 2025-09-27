namespace PdfMcp.Models;

public class PdfPage
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Width { get; set; }
    public double Height { get; set; }
    public int Rotation { get; set; }
    public List<PdfImage> Images { get; set; } = [];
    public List<PdfAnnotation> Annotations { get; set; } = [];
    public List<PdfLink> Links { get; set; } = [];
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}