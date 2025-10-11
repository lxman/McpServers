namespace PdfMcp.Models;

public class PdfLink
{
    public string Type { get; set; } = string.Empty; // Internal, External, Email, etc.
    public string Destination { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}