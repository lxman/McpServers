namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Analysis result for TypeScript variable scope and usage patterns
/// Contains information about variable declarations and usages throughout a file
/// </summary>
public class TypeScriptVariableScopeAnalysis
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string VariableName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<TypeScriptVariableDeclaration> Declarations { get; set; } = [];
    public List<TypeScriptVariableUsage> Usages { get; set; } = [];
}
