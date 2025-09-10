namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Diagnostic information compatible with test expectations
/// </summary>
public class TypeScriptDiagnosticInfo
{
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}
