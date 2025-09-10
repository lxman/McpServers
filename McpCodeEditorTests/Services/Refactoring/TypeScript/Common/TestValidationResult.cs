namespace McpCodeEditorTests.Services.Refactoring.TypeScript.Common;

/// <summary>
/// Test-specific validation result type - renamed to avoid ambiguity with main project ValidationResult
/// </summary>
public class TestPathValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public List<TestDiagnosticInfo> Diagnostics { get; set; } = [];
}

/// <summary>
/// Test-specific diagnostic information
/// </summary>
public class TestDiagnosticInfo
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
