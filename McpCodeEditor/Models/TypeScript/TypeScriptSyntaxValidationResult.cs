namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Result of TypeScript syntax validation
/// REF-003: Comprehensive validation result with detailed error reporting
/// </summary>
public class TypeScriptSyntaxValidationResult
{
    /// <summary>
    /// Whether the syntax validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Main validation message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed syntax errors found
    /// </summary>
    public List<TypeScriptSyntaxError> Errors { get; set; } = [];

    /// <summary>
    /// Syntax warnings (non-blocking issues)
    /// </summary>
    public List<TypeScriptSyntaxWarning> Warnings { get; set; } = [];

    /// <summary>
    /// Validation context that was used
    /// </summary>
    public TypeScriptValidationContext? Context { get; set; }

    /// <summary>
    /// Suggested fixes for syntax errors
    /// </summary>
    public List<TypeScriptSyntaxFix> SuggestedFixes { get; set; } = [];

    /// <summary>
    /// Whether the code would compile with TypeScript compiler
    /// </summary>
    public bool WouldCompile { get; set; }

    /// <summary>
    /// Performance metrics for validation
    /// </summary>
    public TypeScriptValidationMetrics Metrics { get; set; } = new();
}
