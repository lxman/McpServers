namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Represents a TypeScript syntax warning
/// </summary>
public class TypeScriptSyntaxWarning
{
    /// <summary>
    /// Warning code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Warning message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Line number (1-based)
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number (1-based)
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Warning category
    /// </summary>
    public TypeScriptWarningCategory Category { get; set; }
}
