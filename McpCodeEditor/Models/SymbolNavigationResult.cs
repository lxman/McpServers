namespace McpCodeEditor.Models;

public class SymbolNavigationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public List<SymbolLocation> Locations { get; set; } = [];
    public NavigationSymbolInfo? Symbol { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
