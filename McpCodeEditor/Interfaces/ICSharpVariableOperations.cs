using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for C# variable and field operations including introduction and encapsulation
/// </summary>
public interface ICSharpVariableOperations
{
    /// <summary>
    /// Extract a selected expression into a local variable for better code readability
    /// </summary>
    /// <param name="filePath">Path to the C# file</param>
    /// <param name="options">Variable introduction options</param>
    /// <param name="previewOnly">If true, only preview changes without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refactoring result</returns>
    Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        VariableIntroductionOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert public fields to private fields with public properties for better encapsulation
    /// </summary>
    /// <param name="filePath">Path to the C# file</param>
    /// <param name="options">Field encapsulation options</param>
    /// <param name="previewOnly">If true, only preview changes without applying</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refactoring result</returns>
    Task<RefactoringResult> EncapsulateFieldAsync(
        string filePath,
        FieldEncapsulationOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a meaningful variable name from an expression
    /// </summary>
    /// <param name="expression">Expression to analyze</param>
    /// <returns>Generated variable name</returns>
    string GenerateVariableName(string expression);

    /// <summary>
    /// Generate a property name from a field name (PascalCase conversion)
    /// </summary>
    /// <param name="fieldName">Field name to convert</param>
    /// <returns>Generated property name</returns>
    string GeneratePropertyName(string fieldName);

    /// <summary>
    /// Validate if a string is a valid C# identifier
    /// </summary>
    /// <param name="name">Name to validate</param>
    /// <returns>True if valid identifier</returns>
    bool IsValidIdentifier(string name);
}
