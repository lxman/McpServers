namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Introduce a variable result for tests
/// </summary>
public class TypeScriptIntroduceVariableResult
{
    public bool Success { get; set; }
    public string ModifiedCode { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
