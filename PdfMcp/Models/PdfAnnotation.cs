namespace PdfMcp.Models;

public class PdfAnnotation
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}