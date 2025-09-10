using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Context information for TypeScript validation
/// </summary>
public class TypeScriptValidationContext
{
    /// <summary>
    /// File path being validated
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Project type (Angular, React, etc.)
    /// </summary>
    public string ProjectType { get; set; } = string.Empty;

    /// <summary>
    /// Scope where code will be placed
    /// </summary>
    public TypeScriptScopeType TargetScope { get; set; }

    /// <summary>
    /// Expected variable declaration type
    /// </summary>
    public string? ExpectedDeclarationType { get; set; }

    /// <summary>
    /// Whether strict TypeScript checking is enabled
    /// </summary>
    public bool StrictMode { get; set; } = true;

    /// <summary>
    /// Additional validation rules to apply
    /// </summary>
    public List<string> ValidationRules { get; set; } = [];

    /// <summary>
    /// Available imports and declarations in scope
    /// </summary>
    public List<string> AvailableSymbols { get; set; } = [];
}
