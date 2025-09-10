using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for TypeScript variable operations including introduced variable and scope analysis
/// Defines operations for manipulating variables in TypeScript/JavaScript files
/// </summary>
public interface ITypeScriptVariableOperations
{
    Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName = null,
        string declarationType = "const",
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    Task<TypeScriptVariableScopeAnalysis> AnalyzeVariableScopeAsync(
        string filePath,
        string variableName,
        CancellationToken cancellationToken = default);
}
