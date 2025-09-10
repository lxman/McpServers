using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for TypeScript syntax validation service
/// REF-003: Provides comprehensive syntax validation for generated TypeScript code
/// </summary>
public interface ITypeScriptSyntaxValidator
{
    /// <summary>
    /// Validate a TypeScript code snippet for syntax correctness
    /// </summary>
    /// <param name="codeSnippet">TypeScript code to validate</param>
    /// <param name="context">Context information for better validation</param>
    /// <returns>Validation result with detailed error information</returns>
    TypeScriptSyntaxValidationResult ValidateCodeSnippet(string codeSnippet, TypeScriptValidationContext? context = null);

    /// <summary>
    /// Validate a TypeScript variable declaration statement
    /// </summary>
    /// <param name="declaration">Variable declaration to validate</param>
    /// <param name="scopeType">Scope where the declaration will be placed</param>
    /// <returns>Validation result specific to variable declarations</returns>
    TypeScriptSyntaxValidationResult ValidateVariableDeclaration(string declaration, TypeScriptScopeType scopeType);

    /// <summary>
    /// Validate a TypeScript expression
    /// </summary>
    /// <param name="expression">Expression to validate</param>
    /// <param name="expectedType">Expected type context (optional)</param>
    /// <returns>Validation result for the expression</returns>
    TypeScriptSyntaxValidationResult ValidateExpression(string expression, string? expectedType = null);

    /// <summary>
    /// Validate TypeScript identifier naming rules
    /// </summary>
    /// <param name="identifier">Identifier to validate</param>
    /// <returns>Validation result for the identifier</returns>
    TypeScriptSyntaxValidationResult ValidateIdentifier(string identifier);

    /// <summary>
    /// Validate complete TypeScript file content
    /// </summary>
    /// <param name="fileContent">Complete file content to validate</param>
    /// <param name="filePath">File path for context</param>
    /// <returns>Comprehensive validation result</returns>
    Task<TypeScriptSyntaxValidationResult> ValidateFileContentAsync(string fileContent, string filePath);
}
