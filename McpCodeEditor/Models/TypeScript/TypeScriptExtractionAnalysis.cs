namespace McpCodeEditor.Models.TypeScript;

/// <summary>
/// Analysis information for TypeScript method extraction
/// </summary>
public class TypeScriptExtractionAnalysis
{
    public bool HasReturnStatements { get; set; }
    public bool HasAsyncAwait { get; set; }
    public bool HasThisReferences { get; set; }
    public bool HasClosureVariables { get; set; }
    public int CyclomaticComplexity { get; set; }
    public string? ContainingFunctionName { get; set; }
    public string? ContainingClassName { get; set; }
    public List<string> ExternalVariables { get; set; } = [];
    public List<string> ModifiedVariables { get; set; } = [];
    public List<string> SuggestedParameters { get; set; } = [];
    public string? SuggestedReturnType { get; set; }
    public bool HasComplexControlFlow { get; set; }
    public List<string> ImportDependencies { get; set; } = [];
}
