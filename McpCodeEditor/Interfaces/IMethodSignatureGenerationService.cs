using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Models.Validation;

namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service for generating C# method signatures and bodies during method extraction
/// </summary>
public interface IMethodSignatureGenerationService
{
    /// <summary>
    /// Creates an extracted method with proper parameters and signature based on semantic analysis
    /// </summary>
    /// <param name="extractedLines">Lines of code that were extracted</param>
    /// <param name="options">Extraction options containing method details</param>
    /// <param name="returnType">Determined return type for the method</param>
    /// <param name="baseIndentation">Base indentation for the method</param>
    /// <param name="validationResult">Validation result containing semantic analysis</param>
    /// <returns>Complete formatted method with signature and body</returns>
    Task<string> CreateExtractedMethodWithParametersAsync(
        string[] extractedLines, 
        CSharpExtractionOptions options, 
        string returnType, 
        string baseIndentation,
        MethodExtractionValidationResult validationResult);

    /// <summary>
    /// Finds the variables that should be returned as a tuple
    /// </summary>
    /// <param name="extractedLines">Lines of code that were extracted</param>
    /// <param name="tupleType">The tuple type (e.g., "(int, int, string)")</param>
    /// <returns>Comma-separated variable names for tuple return</returns>
    Task<string> FindTupleReturnVariablesAsync(string[] extractedLines, string tupleType);

    /// <summary>
    /// Gets the indentation of a line
    /// </summary>
    /// <param name="line">Line to analyze</param>
    /// <returns>Indentation string (spaces or tabs)</returns>
    string GetLineIndentation(string line);
}
