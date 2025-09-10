using McpCodeEditor.Interfaces;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.CSharp;

/// <summary>
/// Service responsible for code modification operations including content building,
/// line extraction, indentation analysis, and insertion point detection
/// </summary>
public class CodeModificationService : ICodeModificationService
{
    private readonly ILogger<CodeModificationService>? _logger;

    /// <summary>
    /// Initializes a new instance of the CodeModificationService
    /// </summary>
    public CodeModificationService(ILogger<CodeModificationService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string BuildModifiedContent(string[] lines, int startLine, int endLine, string methodCall, string extractedMethod)
    {
        var modifiedLines = new List<string>();

        // Add lines before the extraction
        for (var i = 0; i < startLine - 1; i++)
        {
            modifiedLines.Add(lines[i]);
        }

        // Add the method call
        modifiedLines.Add(methodCall);

        // Add lines after the extraction
        for (int i = endLine; i < lines.Length; i++)
        {
            modifiedLines.Add(lines[i]);
        }

        // Find the correct class closing brace by tracking depth
        int insertionPoint = FindClassClosingBrace(modifiedLines);
    
        // Insert the new method before the class closing brace
        modifiedLines.Insert(insertionPoint, extractedMethod);

        // FIX: Use actual newlines instead of escaped string literals
        return string.Join(Environment.NewLine, modifiedLines);
    }

    /// <inheritdoc />
    public string[] ExtractSelectedLines(string[] lines, int startLine, int endLine)
    {
        int startIndex = startLine - 1; // Convert to 0-based
        int endIndex = endLine - 1;
        var extractedLines = new string[endIndex - startIndex + 1];
        Array.Copy(lines, startIndex, extractedLines, 0, extractedLines.Length);
        return extractedLines;
    }

    /// <inheritdoc />
    public string GetBaseIndentation(string[] lines)
    {
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return GetLineIndentation(line);
            }
        }
        return "";
    }

    /// <inheritdoc />
    public string GetLineIndentation(string line)
    {
        var indentEnd = 0;
        while (indentEnd < line.Length && char.IsWhiteSpace(line[indentEnd]))
        {
            indentEnd++;
        }
        return line[..indentEnd];
    }

    /// <inheritdoc />
    public int FindClassClosingBrace(List<string> lines)
    {
        var braceDepth = 0;
        var foundClass = false;
        var classOpenBraceCount = 0;
    
        for (var i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            string trimmedLine = line.Trim();
        
            // Look for class or struct declaration
            if (!foundClass && (line.Contains("class ") || line.Contains("struct ") || line.Contains("interface ") || line.Contains("record ")))
            {
                foundClass = true;
            }
        
            // Once we've found a class, track its braces
            if (foundClass)
            {
                foreach (char c in line)
                {
                    if (c == '{')
                    {
                        braceDepth++;
                        if (classOpenBraceCount == 0)
                        {
                            classOpenBraceCount = braceDepth;
                        }
                    }
                    else if (c == '}')
                    {
                        braceDepth--;
                    
                        // When we return to the class's opening depth minus one, we've found the class closing brace
                        if (classOpenBraceCount > 0 && braceDepth == classOpenBraceCount - 1)
                        {
                            return i;
                        }
                    }
                }
            }
        }
    
        // Fallback: Find the second-to-last closing brace (assuming namespace wraps class)
        int lastBrace = -1;
        int secondToLastBrace = -1;
    
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].Trim() == "}")
            {
                if (lastBrace == -1)
                {
                    lastBrace = i;
                }
                else if (secondToLastBrace == -1)
                {
                    secondToLastBrace = i;
                    return secondToLastBrace;
                }
            }
        }
    
        // Ultimate fallback
        return Math.Max(0, lines.Count - 2);
    }
}
