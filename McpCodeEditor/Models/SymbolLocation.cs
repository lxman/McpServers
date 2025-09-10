namespace McpCodeEditor.Models;

public class SymbolLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string Preview { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty; // Definition, Reference, Declaration
    public string Context { get; set; } = string.Empty; // Method, Class, Property, etc.
}
