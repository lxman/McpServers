using Microsoft.CodeAnalysis;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Enhanced service for analyzing variable scope and usage patterns in C# method extraction
/// Provides systematic variable classification to replace complex logic in existing services
/// SESSION 2 FIX: Updated signatures to support parameter filtering
/// </summary>
public interface IEnhancedVariableAnalysisService
{
    /// <summary>
    /// Analyzes the scope and classification of variables in the extracted code
    /// </summary>
    /// <param name="extractedLines">Lines of code being extracted</param>
    /// <param name="semanticModel">Semantic model for the document (nullable for fallback analysis)</param>
    /// <param name="syntaxNodes">Syntax nodes representing the extracted code</param>
    /// <returns>Variable scope analysis result</returns>
    Task<VariableScopeAnalysis> AnalyzeVariableScopeAsync(
        string[] extractedLines, 
        SemanticModel? semanticModel, 
        IEnumerable<SyntaxNode> syntaxNodes);

    /// <summary>
    /// Classifies how variables are used within the extraction (read-only, write-only, read-write)
    /// </summary>
    /// <param name="extractedLines">Lines of code being extracted</param>
    /// <param name="semanticModel">Semantic model for the document (nullable for fallback analysis)</param>
    /// <param name="syntaxNodes">Syntax nodes representing the extracted code</param>
    /// <returns>Variable usage classification result</returns>
    Task<VariableUsageClassification> ClassifyVariableUsageAsync(
        string[] extractedLines, 
        SemanticModel? semanticModel, 
        IEnumerable<SyntaxNode> syntaxNodes);

    /// <summary>
    /// Generates a mapping of variables to their proper handling strategy
    /// SESSION 2 FIX: Added optional parameters for better parameter filtering
    /// </summary>
    /// <param name="scopeAnalysis">Result from variable scope analysis</param>
    /// <param name="usageClassification">Result from variable usage classification</param>
    /// <param name="extractedLines">Optional: Lines being extracted for context</param>
    /// <param name="fullFileLines">Optional: Full file content for better context</param>
    /// <returns>Variable mapping for code generation</returns>
    VariableHandlingMapping GenerateVariableMapping(
        VariableScopeAnalysis scopeAnalysis, 
        VariableUsageClassification usageClassification,
        string[]? extractedLines = null,
        string[]? fullFileLines = null);

    /// <summary>
    /// Performs comprehensive variable analysis combining scope, usage, and mapping
    /// CRITICAL FIX: Now accepts nullable SemanticModel to fix integration issues
    /// SESSION 2 FIX: Added fullFileLines parameter for better parameter filtering
    /// </summary>
    /// <param name="extractedLines">Lines of code being extracted</param>
    /// <param name="semanticModel">Semantic model for the document (nullable for fallback analysis)</param>
    /// <param name="syntaxNodes">Syntax nodes representing the extracted code</param>
    /// <param name="fullFileLines">Optional: Full file content for better context</param>
    /// <returns>Complete variable analysis result</returns>
    Task<EnhancedVariableAnalysisResult> PerformCompleteAnalysisAsync(
        string[] extractedLines, 
        SemanticModel? semanticModel, 
        IEnumerable<SyntaxNode> syntaxNodes,
        string[]? fullFileLines = null);
}

/// <summary>
/// Result of variable scope analysis
/// </summary>
public class VariableScopeAnalysis
{
    /// <summary>
    /// Variables declared within the extraction that need to be returned
    /// </summary>
    public List<VariableInfo> LocalVariables { get; set; } = [];

    /// <summary>
    /// Variables that exist outside the extraction and are used within it
    /// </summary>
    public List<VariableInfo> ExternalVariables { get; set; } = [];

    /// <summary>
    /// Variables that should be passed as parameters to the extracted method
    /// </summary>
    public List<VariableInfo> ParameterVariables { get; set; } = [];

    /// <summary>
    /// Variables that are modified within the extraction
    /// </summary>
    public List<VariableInfo> ModifiedVariables { get; set; } = [];
}

/// <summary>
/// Result of variable usage classification
/// </summary>
public class VariableUsageClassification
{
    /// <summary>
    /// Variables that are only read from (candidates for parameters)
    /// </summary>
    public List<VariableInfo> ReadOnlyVariables { get; set; } = [];

    /// <summary>
    /// Variables that are only written to (candidates for return values)
    /// </summary>
    public List<VariableInfo> WriteOnlyVariables { get; set; } = [];

    /// <summary>
    /// Variables that are both read from and written to
    /// </summary>
    public List<VariableInfo> ReadWriteVariables { get; set; } = [];
}

/// <summary>
/// Mapping of variables to their handling strategy
/// </summary>
public class VariableHandlingMapping
{
    /// <summary>
    /// Variables that should be passed as method parameters
    /// </summary>
    public List<VariableInfo> ParametersToPass { get; set; } = [];

    /// <summary>
    /// Variables that should be declared using 'var' in method call
    /// </summary>
    public List<VariableInfo> VariablesToDeclare { get; set; } = [];

    /// <summary>
    /// Variables that should be assigned in method call (existing variables)
    /// </summary>
    public List<VariableInfo> VariablesToAssign { get; set; } = [];

    /// <summary>
    /// Variables that should be returned from the extracted method
    /// </summary>
    public List<VariableInfo> VariablesToReturn { get; set; } = [];

    /// <summary>
    /// Suggested return type for the extracted method
    /// </summary>
    public string SuggestedReturnType { get; set; } = "void";

    /// <summary>
    /// Whether the method call should use tuple destructuring
    /// </summary>
    public bool RequiresTupleDestructuring => VariablesToReturn.Count > 1;

    /// <summary>
    /// Whether the method call should declare new variables
    /// </summary>
    public bool RequiresVariableDeclaration => VariablesToDeclare.Count > 0;
}

/// <summary>
/// Complete enhanced variable analysis result
/// </summary>
public class EnhancedVariableAnalysisResult
{
    /// <summary>
    /// Variable scope analysis results
    /// </summary>
    public VariableScopeAnalysis ScopeAnalysis { get; set; } = new();

    /// <summary>
    /// Variable usage classification results
    /// </summary>
    public VariableUsageClassification UsageClassification { get; set; } = new();

    /// <summary>
    /// Variable handling mapping for code generation
    /// </summary>
    public VariableHandlingMapping HandlingMapping { get; set; } = new();

    /// <summary>
    /// Analysis success status
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Any errors encountered during analysis
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Analysis warnings
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    public List<VariableInfo> ModifiedVariables { get; set; } = [];
    
    public bool RequiresMultipleReturns { get; set; }
    
    public bool RequiresTupleDestructuring { get; set; }
}

/// <summary>
/// Information about a variable identified during analysis
/// </summary>
public record VariableInfo
{
    /// <summary>
    /// Variable name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Variable type (if known)
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Whether the variable is declared within the extraction
    /// </summary>
    public bool IsDeclaredInExtraction { get; init; }

    /// <summary>
    /// Whether the variable is used after the extraction
    /// </summary>
    public bool IsUsedAfterExtraction { get; set; }

    /// <summary>
    /// Whether the variable is modified within the extraction
    /// </summary>
    public bool IsModified { get; init; }

    /// <summary>
    /// Scope level of the variable (local, class, global)
    /// </summary>
    public VariableScope Scope { get; init; }

    /// <summary>
    /// Usage pattern of the variable
    /// </summary>
    public VariableUsagePattern UsagePattern { get; init; }
}

/// <summary>
/// Variable scope enumeration
/// </summary>
public enum VariableScope
{
    Local,
    External,
    Parameter,
    Field,
    Property,
    Static,
    Instance
}

/// <summary>
/// Variable usage pattern enumeration
/// </summary>
public enum VariableUsagePattern
{
    ReadOnly,
    WriteOnly,
    ReadWrite
}
