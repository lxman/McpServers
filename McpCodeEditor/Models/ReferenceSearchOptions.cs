namespace McpCodeEditor.Models;

public class ReferenceSearchOptions
{
    public bool IncludeDeclaration { get; set; } = true;
    public bool IncludeDefinitions { get; set; } = true;
    public bool IncludeReferences { get; set; } = true;
    public bool IncludeOverrides { get; set; } = false;
    public bool IncludeImplementations { get; set; } = false;
    public bool LimitToCurrentProject { get; set; } = false;
    public int MaxResults { get; set; } = 1000;
    public string[]? FilePatterns { get; set; }
}
