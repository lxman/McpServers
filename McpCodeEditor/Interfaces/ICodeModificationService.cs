namespace McpCodeEditor.Interfaces;

/// <summary>
/// Service responsible for code modification operations including content building,
/// line extraction, indentation analysis, and insertion point detection
/// </summary>
public interface ICodeModificationService
{
    /// <summary>
    /// Builds the modified content by replacing extracted lines with method call and adding the extracted method
    /// </summary>
    /// <param name="lines">Original source code lines</param>
    /// <param name="startLine">Start line of extraction (1-based)</param>
    /// <param name="endLine">End line of extraction (1-based)</param>
    /// <param name="methodCall">Generated method call to replace extracted code</param>
    /// <param name="extractedMethod">Generated method to insert into class</param>
    /// <returns>Modified source code content</returns>
    string BuildModifiedContent(string[] lines, int startLine, int endLine, string methodCall, string extractedMethod);

    /// <summary>
    /// Extracts the selected lines from the source code array
    /// </summary>
    /// <param name="lines">Source code lines</param>
    /// <param name="startLine">Start line (1-based)</param>
    /// <param name="endLine">End line (1-based)</param>
    /// <returns>Array of extracted lines</returns>
    string[] ExtractSelectedLines(string[] lines, int startLine, int endLine);

    /// <summary>
    /// Gets the base indentation from the first non-empty line
    /// </summary>
    /// <param name="lines">Lines to analyze</param>
    /// <returns>Base indentation string</returns>
    string GetBaseIndentation(string[] lines);

    /// <summary>
    /// Gets the indentation of a specific line
    /// </summary>
    /// <param name="line">Line to analyze</param>
    /// <returns>Indentation string (whitespace prefix)</returns>
    string GetLineIndentation(string line);

    /// <summary>
    /// Finds the appropriate insertion point for the extracted method within a class
    /// </summary>
    /// <param name="lines">Source code lines</param>
    /// <returns>Line index where the extracted method should be inserted</returns>
    int FindClassClosingBrace(List<string> lines);
}
