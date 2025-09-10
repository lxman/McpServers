namespace McpCodeEditorTests.Services.Refactoring.TypeScript.Common;

public class VariableDeclarationResult
{
    public bool Success { get; set; }
    public string Declaration { get; set; } = string.Empty;
    public string DeclarationType { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool SuggestClassProperty { get; set; }
}