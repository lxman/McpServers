namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Suggested fix for a syntax error
/// </summary>
public class TypeScriptSyntaxFix
{
    /// <summary>
    /// Description of the fix
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Original problematic code
    /// </summary>
    public string OriginalCode { get; set; } = string.Empty;

    /// <summary>
    /// Suggested fixed code
    /// </summary>
    public string FixedCode { get; set; } = string.Empty;

    /// <summary>
    /// Line number to apply the fix
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column range for the fix
    /// </summary>
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }

    /// <summary>
    /// Confidence in the suggested fix (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Type of fix being suggested
    /// </summary>
    public TypeScriptFixType FixType { get; set; }
}
