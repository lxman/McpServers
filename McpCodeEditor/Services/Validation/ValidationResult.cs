namespace McpCodeEditor.Services.Validation;

/// <summary>
/// Legacy validation result type for test compatibility
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<DiagnosticInfo> Diagnostics { get; set; } = [];
}

/// <summary>
/// Diagnostic information for validation results
/// </summary>
public class DiagnosticInfo
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
