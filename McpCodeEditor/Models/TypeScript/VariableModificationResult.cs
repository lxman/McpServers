namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Result of applying variable modifications to code
/// Extracted from TypeScriptVariableOperations for better organization
/// </summary>
public class VariableModificationResult
{
    public bool Success { get; set; }
    public List<string> ModifiedLines { get; set; } = [];
    public int InsertionLine { get; set; }
    public int UpdatedLine { get; set; }
    public string VariableReference { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
