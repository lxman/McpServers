namespace McpCodeEditor.Models.Refactoring.CSharp;

/// <summary>
/// Options for introducing a variable in C# code
/// </summary>
public class VariableIntroductionOptions
{
    /// <summary>
    /// Line number containing the expression (1-based)
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Starting column of the expression (1-based)
    /// </summary>
    public int StartColumn { get; set; }

    /// <summary>
    /// Ending column of the expression (1-based)
    /// </summary>
    public int EndColumn { get; set; }

    /// <summary>
    /// Name for the new variable (optional - will be auto-generated if not provided)
    /// </summary>
    public string? VariableName { get; set; }

    /// <summary>
    /// Type of variable declaration to use (var, specific type, etc.)
    /// </summary>
    public string VariableType { get; set; } = "var";

    /// <summary>
    /// Scope for the variable introduction (method, block, etc.)
    /// </summary>
    public VariableScope Scope { get; set; } = VariableScope.Block;

    /// <summary>
    /// Whether to make the variable const if the expression is a constant
    /// </summary>
    public bool PreferConst { get; set; } = false;

    /// <summary>
    /// Whether to add comments explaining the variable introduction
    /// </summary>
    public bool AddComments { get; set; } = false;

    /// <summary>
    /// Whether to validate the expression before extraction
    /// </summary>
    public bool ValidateExpression { get; set; } = true;

    /// <summary>
    /// Maximum length of expression to consider for variable introduction
    /// </summary>
    public int MaxExpressionLength { get; set; } = 200;

    /// <summary>
    /// Minimum length of expression to consider for variable introduction
    /// </summary>
    public int MinExpressionLength { get; set; } = 3;

    /// <summary>
    /// Custom indentation to use (if null, will detect from context)
    /// </summary>
    public string? CustomIndentation { get; set; }

    /// <summary>
    /// Whether to analyze potential naming conflicts
    /// </summary>
    public bool CheckNamingConflicts { get; set; } = true;
}

/// <summary>
/// Scope for variable introduction
/// </summary>
public enum VariableScope
{
    /// <summary>
    /// Introduce variable in the current block
    /// </summary>
    Block,

    /// <summary>
    /// Introduce variable at method level
    /// </summary>
    Method,

    /// <summary>
    /// Introduce variable at class level (as field)
    /// </summary>
    Class
}
