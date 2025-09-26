using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpCodeEditor.Services.Utilities;

/// <summary>
/// Utility service for code formatting, indentation, and layout operations.
/// Extracted from RefactoringService as part of SOLID refactoring (Slice VIII).
/// </summary>
public static class CodeFormatting
{
    /// <summary>
    /// Helper method to get the indentation of a line
    /// </summary>
    public static string GetLineIndentation(string line)
    {
        var indentEnd = 0;
        while (indentEnd < line.Length && char.IsWhiteSpace(line[indentEnd]))
        {
            indentEnd++;
        }
        return line[..indentEnd];
    }

    /// <summary>
    /// Helper method to get the indentation of a statement
    /// </summary>
    public static string GetStatementIndentation(StatementSyntax statement, string sourceCode)
    {
        string[] lines = sourceCode.Split('\n');
        int statementStart = statement.SpanStart;

        // Find the line containing the statement
        var currentPosition = 0;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (currentPosition <= statementStart && statementStart < currentPosition + lines[lineIndex].Length + 1)
            {
                // Found the line, extract indentation
                return GetLineIndentation(lines[lineIndex]);
            }
            currentPosition += lines[lineIndex].Length + 1; // +1 for newline
        }

        return "";
    }

    /// <summary>
    /// Check if an identifier is within a field declaration (to avoid replacing field names in their own declarations)
    /// </summary>
    public static bool IsInFieldDeclaration(IdentifierNameSyntax identifier)
    {
        return identifier.FirstAncestorOrSelf<FieldDeclarationSyntax>() != null;
    }
}
