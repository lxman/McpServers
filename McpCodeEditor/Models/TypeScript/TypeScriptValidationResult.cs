namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Result types that match the test expectations for TypeScript variable operations
/// These need to be compatible with the test project's result types for reflection-based calls
/// </summary>

/// <summary>
/// Validation result compatible with test expectations
/// </summary>
public class TypeScriptValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<TypeScriptDiagnosticInfo> Diagnostics { get; set; } = [];
    public TypeScriptExtractionAnalysis? Analysis { get; set; }
}
