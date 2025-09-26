using System.Text.RegularExpressions;

namespace McpCodeEditor.Services.Utilities;

/// <summary>
/// Utility service for syntax parsing and manipulation operations.
/// Extracted from RefactoringService as part of SOLID refactoring (Slice VIII).
/// </summary>
public static class SyntaxHelpers
{
    /// <summary>
    /// Parse TypeScript import statements from file lines
    /// </summary>
    public static List<TypeScriptImport> ParseTypeScriptImports(string[] lines)
    {
        var imports = new List<TypeScriptImport>();

        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("/*"))
                continue;

            // Check for import statements
            if (line.StartsWith("import "))
            {
                imports.Add(new TypeScriptImport
                {
                    LineNumber = i,
                    OriginalText = lines[i],
                    ImportType = DetermineImportType(line),
                    ModuleName = ExtractModuleName(line)
                });
            }
        }

        return imports;
    }

    /// <summary>
    /// Determine the type of TypeScript import (library, relative, etc.)
    /// </summary>
    public static TypeScriptImportType DetermineImportType(string importLine)
    {
        // Extract the module path from the import statement
        Match match = Regex.Match(
            importLine,
            @"from\s+['""]([^'""]+)['""]\s"
        );

        if (!match.Success) return TypeScriptImportType.Other;

        string modulePath = match.Groups[1].Value;

        if (modulePath.StartsWith("./") || modulePath.StartsWith("../"))
            return TypeScriptImportType.Relative;

        if (modulePath.StartsWith("@/"))
            return TypeScriptImportType.Alias;

        if (modulePath.StartsWith("@"))
            return TypeScriptImportType.ScopedLibrary;

        return TypeScriptImportType.Library;
    }

    /// <summary>
    /// Extract module name from import statement
    /// </summary>
    public static string ExtractModuleName(string importLine)
    {
        Match match = Regex.Match(
            importLine,
            @"from\s+['""]([^'""]+)['""]\s"
        );

        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>
    /// Organize import statements by sorting and grouping
    /// </summary>
    public static List<TypeScriptImport> OrganizeImportStatements(
        List<TypeScriptImport> imports,
        bool sortAlphabetically,
        bool groupByType)
    {
        if (!groupByType && !sortAlphabetically)
            return imports;

        if (groupByType)
        {
            // Group by import type
            IOrderedEnumerable<IGrouping<TypeScriptImportType, TypeScriptImport>> grouped = imports
                .GroupBy(i => i.ImportType)
                .OrderBy(g => (int)g.Key); // Sort groups by type enum value

            var organized = new List<TypeScriptImport>();
            foreach (IGrouping<TypeScriptImportType, TypeScriptImport> group in grouped)
            {
                List<TypeScriptImport> groupImports = group.ToList();
                if (sortAlphabetically)
                {
                    groupImports = groupImports.OrderBy(i => i.ModuleName).ToList();
                }
                organized.AddRange(groupImports);
            }
            return organized;
        }
        else if (sortAlphabetically)
        {
            return imports.OrderBy(i => i.ModuleName).ToList();
        }

        return imports;
    }

    /// <summary>
    /// Check if a line is an import statement or empty
    /// </summary>
    public static bool IsImportOrEmpty(string line)
    {
        string trimmed = line.Trim();
        return string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("import ");
    }

    /// <summary>
    /// Determine variable declaration type for TypeScript (const, let, or var)
    /// </summary>
    public static string DetermineTypeScriptVariableDeclarationType(string expression)
    {
        // Simple heuristics for determining declaration type
        string cleaned = expression.Trim();

        // Use const for literals and simple values
        if (cleaned.StartsWith('"') && cleaned.EndsWith('"') ||
            cleaned.StartsWith("'") && cleaned.EndsWith("'") ||
            cleaned.StartsWith("`") && cleaned.EndsWith("`") ||
            int.TryParse(cleaned, out _) || double.TryParse(cleaned, out _) ||
            cleaned == "true" || cleaned == "false" || cleaned == "null" || cleaned == "undefined")
        {
            return "const";
        }

        // Use let for most other cases (allows reassignment)
        return "let";
    }

    /// <summary>
    /// Find TypeScript function definition
    /// </summary>
    public static TypeScriptFunctionInfo? FindTypeScriptFunction(string[] lines, string functionName)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // Check for various function declaration patterns
            if (IsTypeScriptFunctionDeclaration(line, functionName))
            {
                return ParseTypeScriptFunctionInfo(lines, i, functionName);
            }
        }

        return null;
    }

    /// <summary>
    /// Extract TypeScript function body
    /// </summary>
    public static string ExtractTypeScriptFunctionBody(string[] lines, TypeScriptFunctionInfo functionInfo)
    {
        var bodyLines = new List<string>();

        // Find the opening brace and extract body content
        var inBody = false;
        var braceCount = 0;

        for (int i = functionInfo.StartLine; i <= functionInfo.EndLine && i < lines.Length; i++)
        {
            string line = lines[i];

            if (!inBody)
            {
                // Look for opening brace
                if (line.Contains('{'))
                {
                    inBody = true;
                    braceCount = 1;
                    
                    // Get content after opening brace
                    int braceIndex = line.IndexOf('{');
                    string contentAfterBrace = line[(braceIndex + 1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(contentAfterBrace))
                    {
                        bodyLines.Add(contentAfterBrace);
                    }
                }
                continue;
            }

            // Count braces to find the end of function body
            for (var j = 0; j < line.Length; j++)
            {
                if (line[j] == '{') braceCount++;
                else if (line[j] == '}') braceCount--;

                if (braceCount == 0)
                {
                    // End of function body - get content before closing brace
                    string contentBeforeBrace = line[..j].Trim();
                    if (!string.IsNullOrWhiteSpace(contentBeforeBrace))
                    {
                        bodyLines.Add(contentBeforeBrace);
                    }
                    goto BodyComplete;
                }
            }

            // If we haven't reached the end, add the whole line
            if (braceCount > 0)
            {
                bodyLines.Add(line);
            }
        }

        BodyComplete:
        return string.Join("\n", bodyLines).Trim();
    }

    /// <summary>
    /// Find TypeScript function call sites
    /// </summary>
    public static List<TypeScriptFunctionCall> FindTypeScriptFunctionCalls(string[] lines, string functionName)
    {
        var calls = new List<TypeScriptFunctionCall>();

        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Simple pattern matching for function calls
            var pattern = $@"\b{Regex.Escape(functionName)}\s*\(";
            MatchCollection matches = Regex.Matches(line, pattern);

            foreach (Match match in matches)
            {
                calls.Add(new TypeScriptFunctionCall
                {
                    LineNumber = i,
                    ColumnStart = match.Index,
                    ColumnEnd = match.Index + match.Length - 1,
                    OriginalText = line
                });
            }
        }

        return calls;
    }

    /// <summary>
    /// Replace TypeScript function call with function body
    /// </summary>
    public static void ReplaceTypeScriptFunctionCall(List<string> lines, TypeScriptFunctionCall call, string functionBody)
    {
        if (call.LineNumber >= lines.Count) return;

        string originalLine = lines[call.LineNumber];
        
        // Get indentation from the original line
        var indentation = "";
        for (var i = 0; i < originalLine.Length && char.IsWhiteSpace(originalLine[i]); i++)
        {
            indentation += originalLine[i];
        }

        // Replace the function call with the function body
        string[] bodyLines = functionBody.Split('\n');
        
        // Remove the original line
        lines.RemoveAt(call.LineNumber);

        // Insert the function body lines with proper indentation
        for (int i = bodyLines.Length - 1; i >= 0; i--)
        {
            string bodyLine = bodyLines[i];
            string indentedLine = string.IsNullOrWhiteSpace(bodyLine) ? bodyLine : indentation + bodyLine.Trim();
            lines.Insert(call.LineNumber, indentedLine);
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Check if a line contains a TypeScript function declaration
    /// </summary>
    private static bool IsTypeScriptFunctionDeclaration(string line, string functionName)
    {
        // Check for various TypeScript function patterns
        var patterns = new[]
        {
            $@"\bfunction\s+{Regex.Escape(functionName)}\s*\(",          // function functionName(
            $@"\b{Regex.Escape(functionName)}\s*:\s*\([^)]*\)\s*=>\s*{{", // functionName: () => {
            $@"\b{Regex.Escape(functionName)}\s*=\s*\([^)]*\)\s*=>\s*{{", // functionName = () => {
            $@"\b{Regex.Escape(functionName)}\s*=\s*function\s*\(",       // functionName = function(
            $@"\b{Regex.Escape(functionName)}\s*\([^)]*\)\s*{{",          // functionName() {
        };

        return patterns.Any(pattern => Regex.IsMatch(line, pattern));
    }

    /// <summary>
    /// Parse TypeScript function information from lines
    /// </summary>
    private static TypeScriptFunctionInfo ParseTypeScriptFunctionInfo(string[] lines, int startLine, string functionName)
    {
        var functionInfo = new TypeScriptFunctionInfo
        {
            Name = functionName,
            StartLine = startLine,
            Parameters = [],
            IsAsync = lines[startLine].Contains("async"),
            HasReturnValue = false // Simplified - would need better parsing
        };

        // Find the end of the function by counting braces
        var braceCount = 0;
        var foundOpenBrace = false;

        for (int i = startLine; i < lines.Length; i++)
        {
            string line = lines[i];

            foreach (char c in line)
            {
                if (c == '{')
                {
                    braceCount++;
                    foundOpenBrace = true;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (foundOpenBrace && braceCount == 0)
                    {
                        functionInfo.EndLine = i;
                        return functionInfo;
                    }
                }
            }
        }

        // If we couldn't find the end, set it to the last line
        functionInfo.EndLine = lines.Length - 1;
        return functionInfo;
    }

    #endregion
}

#region TypeScript Model Classes

/// <summary>
/// TypeScript import information
/// </summary>
public class TypeScriptImport
{
    public int LineNumber { get; set; }
    public string OriginalText { get; set; } = "";
    public TypeScriptImportType ImportType { get; set; }
    public string ModuleName { get; set; } = "";
}

/// <summary>
/// TypeScript import types
/// </summary>
public enum TypeScriptImportType
{
    Library,
    ScopedLibrary,
    Relative,
    Alias,
    Other
}

/// <summary>
/// TypeScript function information
/// </summary>
public class TypeScriptFunctionInfo
{
    public string Name { get; set; } = "";
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public List<string> Parameters { get; set; } = [];
    public bool IsAsync { get; set; }
    public bool HasReturnValue { get; set; }
    public TypeScriptFunctionType FunctionType { get; set; } = TypeScriptFunctionType.Function;
}

/// <summary>
/// TypeScript function types
/// </summary>
public enum TypeScriptFunctionType
{
    Function,
    Arrow,
    Method,
    AsyncFunction,
    AsyncArrow
}

/// <summary>
/// TypeScript function call information
/// </summary>
public class TypeScriptFunctionCall
{
    public int LineNumber { get; set; }
    public int ColumnStart { get; set; }
    public int ColumnEnd { get; set; }
    public string OriginalText { get; set; } = "";
}

#endregion
