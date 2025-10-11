namespace OfficeReader.Models;

public class ExtractedImage
{
    public string Source { get; set; } = "";
    public string Name { get; set; } = "";
    public string Format { get; set; } = "";
    public double Width { get; set; }
    public double Height { get; set; }
}