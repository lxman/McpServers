namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Represents a specific TypeScript syntax error
/// </summary>
public class TypeScriptSyntaxError
{
    /// <summary>
    /// Error code (e.g., TS2304, TS1005)
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Line number where error occurs (1-based)
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number where error occurs (1-based)
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Length of the problematic text
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// The problematic text that caused the error
    /// </summary>
    public string ProblematicText { get; set; } = string.Empty;

    /// <summary>
    /// Severity of the error
    /// </summary>
    public TypeScriptErrorSeverity Severity { get; set; } = TypeScriptErrorSeverity.Error;

    /// <summary>
    /// Category of the syntax error
    /// </summary>
    public TypeScriptErrorCategory Category { get; set; }
}
