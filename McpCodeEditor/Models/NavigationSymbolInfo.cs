namespace McpCodeEditor.Models;

public class NavigationSymbolInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string ContainingType { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
    public bool IsFromSource { get; set; }
    public string? Documentation { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}
