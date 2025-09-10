namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Scope detection result for tests
/// </summary>
public class TypeScriptScopeDetectionResult
{
    public bool Success { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? MethodName { get; set; }
    public string? ErrorMessage { get; set; }
}
