using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Services.TypeScript.Interfaces;

/// <summary>
/// Service for detecting and adjusting expression boundaries in TypeScript/JavaScript code
/// Handles AST-based parsing, pattern-based detection, and smart boundary expansion
/// Part of Phase A2 refactoring - extracted from TypeScriptVariableOperations
/// </summary>
public interface IExpressionBoundaryDetectionService
{
    /// <summary>
    /// Detect and adjust expression boundaries using multiple strategies
    /// </summary>
    /// <param name="lineContent">The line of code containing the expression</param>
    /// <param name="startColumn">Starting column (1-based) of the selection</param>
    /// <param name="endColumn">Ending column (1-based) of the selection</param>
    /// <returns>Result containing the detected expression and adjusted boundaries</returns>
    ExpressionBoundaryResult DetectExpressionBoundaries(string lineContent, int startColumn, int endColumn);
    
    /// <summary>
    /// Validate if the provided text is a valid TypeScript expression
    /// </summary>
    /// <param name="expression">The expression text to validate</param>
    /// <returns>True if the expression is valid TypeScript syntax</returns>
    bool IsValidTypeScriptExpression(string expression);
    
    /// <summary>
    /// Generate a meaningful variable name from the given expression
    /// </summary>
    /// <param name="expression">The expression to generate a name from</param>
    /// <returns>A camelCase variable name suitable for TypeScript</returns>
    string GenerateVariableName(string expression);
}
