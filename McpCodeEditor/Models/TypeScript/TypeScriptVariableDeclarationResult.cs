namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Variable declaration result for tests
/// </summary>
public class TypeScriptVariableDeclarationResult
{
    public bool Success { get; set; }
    public string Declaration { get; set; } = string.Empty;
    public string DeclarationType { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool SuggestClassProperty { get; set; }
}
