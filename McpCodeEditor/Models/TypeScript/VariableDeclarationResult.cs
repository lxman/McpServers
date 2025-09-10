using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Result of scope-aware variable declaration generation
/// Extracted from TypeScriptVariableOperations for better organization
/// </summary>
public class VariableDeclarationResult
{
    public bool Success { get; set; }
    public string Declaration { get; set; } = string.Empty;
    public string DeclarationType { get; set; } = string.Empty;
    public TypeScriptVariablePlacementStrategy PlacementStrategy { get; set; } = new();
    public bool RequiresIndentation { get; set; }
    public string SyntaxNote { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool SuggestClassProperty { get; set; }
}
