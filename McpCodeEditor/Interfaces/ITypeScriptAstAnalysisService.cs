using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for TypeScript AST analysis operations
/// Phase A2-T2: Extracted from TypeScriptVariableOperations for focused AST parsing and analysis
/// Simplified interface focusing on core functionality needed by TypeScriptVariableOperations
/// </summary>
public interface ITypeScriptAstAnalysisService
{
    /// <summary>
    /// Validate if a code snippet is a valid TypeScript expression
    /// This is the primary method used by TypeScriptVariableOperations
    /// </summary>
    /// <param name="expression">Expression to validate</param>
    /// <returns>True if valid expression, false otherwise</returns>
    bool IsValidTypeScriptExpression(string expression);

    /// <summary>
    /// Infer the type of a TypeScript expression from pattern analysis
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <param name="scopeContext">Scope context for type inference</param>
    /// <returns>Inferred type information</returns>
    TypeScriptTypeInference InferExpressionType(string expression, string scopeContext);
}
