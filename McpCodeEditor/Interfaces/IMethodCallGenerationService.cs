using McpCodeEditor.Models.Validation;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for generating C# method calls during method extraction
/// </summary>
public interface IMethodCallGenerationService
{
    /// <summary>
    /// Creates method call with proper parameters based on semantic analysis
    /// </summary>
    /// <param name="indentation">Base indentation for the method call</param>
    /// <param name="methodName">Name of the method to call</param>
    /// <param name="validationResult">Validation result containing semantic analysis</param>
    /// <param name="extractedLines">Lines of code that were extracted</param>
    /// <returns>Formatted method call with appropriate variable assignment</returns>
    Task<string> CreateMethodCallAsync(string indentation, string methodName, MethodExtractionValidationResult validationResult, string[] extractedLines);

    /// <summary>
    /// Extracts variable names for tuple return destructuring
    /// </summary>
    /// <param name="extractedLines">Lines of code that were extracted</param>
    /// <param name="tupleType">The tuple type (e.g., "(int, int, string)")</param>
    /// <returns>Comma-separated variable names for tuple destructuring</returns>
    Task<string> ExtractTupleVariableNamesAsync(string[] extractedLines, string tupleType);

    /// <summary>
    /// Finds the main variable that should capture the return value
    /// </summary>
    /// <param name="extractedLines">Lines of code that were extracted</param>
    /// <param name="returnType">Expected return type</param>
    /// <returns>Variable name that should capture the return value, or null if not found</returns>
    Task<string?> FindMainVariableAsync(string[] extractedLines, string returnType);

    /// <summary>
    /// Checks if a string is a C# keyword or static member that shouldn't be a parameter
    /// </summary>
    /// <param name="name">Name to check</param>
    /// <returns>True if the name is a keyword or static member</returns>
    bool IsKeywordOrStaticMember(string name);
}
