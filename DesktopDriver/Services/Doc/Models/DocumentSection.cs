namespace DesktopDriver.Services.Doc.Models;

public class DocumentSection
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Level { get; set; } // For headers: 1=H1, 2=H2, etc.
    public int StartPosition { get; set; }
    public int Length { get; set; }
    public Dictionary<string, object> Data { get; set; } = new(); // Tables, charts, etc.
    public List<DocumentSection> SubSections { get; set; } = [];
}