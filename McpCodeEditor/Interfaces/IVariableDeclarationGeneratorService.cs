using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for generating TypeScript variable declarations with proper scope-aware syntax
/// Phase A2-T3: Extracted from TypeScriptVariableOperations for better separation of concerns
/// </summary>
public interface IVariableDeclarationGeneratorService
{
    /// <summary>
    /// Generate scope-aware variable declaration with proper TypeScript syntax
    /// Modern approach that uses TypeScriptScopeAnalysisResult for context
    /// </summary>
    VariableDeclarationResult GenerateScopeAwareVariableDeclaration(
        TypeScriptScopeAnalysisResult scopeAnalysis, 
        string variableName, 
        string selectedExpression, 
        string requestedDeclarationType);

    /// <summary>
    /// REF-002 FIX: Generate variable declaration with proper scope-aware syntax
    /// Legacy method for test compatibility - returns anonymous object matching test expectations
    /// </summary>
    object GenerateVariableDeclaration(
        string variableName,
        string expression,
        string scopeContext,
        int insertionLine,
        string? typeAnnotation = null);

    /// <summary>
    /// REF-002 FIX: Validate variable declaration syntax based on scope context
    /// Legacy method for test compatibility - returns anonymous object matching test expectations
    /// </summary>
    object ValidateVariableDeclaration(string declaration, string scopeContext);

    /// <summary>
    /// Validate if the requested declaration type is valid for TypeScript
    /// </summary>
    bool IsValidDeclarationType(string declarationType);
}