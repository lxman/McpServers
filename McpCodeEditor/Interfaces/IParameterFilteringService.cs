namespace McpCodeEditor.Interfaces;

/// <summary>
/// Interface for filtering and determining which variables should be passed as parameters
/// SESSION 2: Created to properly distinguish between class fields and local variables
/// </summary>
public interface IParameterFilteringService
{
    /// <summary>
    /// Filters variables to determine which should be passed as parameters
    /// </summary>
    /// <param name="externalVariables">List of external variables found in the extraction</param>
    /// <param name="extractedLines">The lines being extracted</param>
    /// <param name="fullFileLines">Optional: Full file content for better context</param>
    /// <returns>Filtered list of variables that should be passed as parameters</returns>
    List<VariableInfo> FilterParametersToPass(
        List<VariableInfo> externalVariables,
        string[] extractedLines,
        string[]? fullFileLines = null);

    /// <summary>
    /// Determines if a specific variable should be passed as a parameter
    /// </summary>
    bool ShouldPassAsParameter(VariableInfo variable, string[] extractedLines, string[]? fullFileLines);

    /// <summary>
    /// Gets suggested parameter types based on usage patterns
    /// </summary>
    Dictionary<string, string> SuggestParameterTypes(List<VariableInfo> parameters, string[] extractedLines);
}
