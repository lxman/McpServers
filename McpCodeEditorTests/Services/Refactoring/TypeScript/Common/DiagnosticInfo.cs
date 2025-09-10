namespace McpCodeEditorTests.Services.Refactoring.TypeScript.Common;

/// <summary>
/// Represents diagnostic information for TypeScript analysis and refactoring operations
/// </summary>
public class DiagnosticInfo
{
    /// <summary>
    /// The severity level of the diagnostic
    /// </summary>
    public DiagnosticSeverity Severity { get; set; }
    
    /// <summary>
    /// The diagnostic message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// The line number where the diagnostic occurs (1-based)
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// The column number where the diagnostic occurs (1-based)
    /// </summary>
    public int ColumnNumber { get; set; }
    
    /// <summary>
    /// The diagnostic code or identifier
    /// </summary>
    public string? Code { get; set; }
    
    /// <summary>
    /// The source of the diagnostic (e.g., "TypeScript", "ESLint", etc.)
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// Additional context or details about the diagnostic
    /// </summary>
    public string? Context { get; set; }
}

/// <summary>
/// Represents the severity level of a diagnostic
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational message
    /// </summary>
    Information = 0,
    
    /// <summary>
    /// Warning message
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Error message
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Hint or suggestion
    /// </summary>
    Hint = 3
}
