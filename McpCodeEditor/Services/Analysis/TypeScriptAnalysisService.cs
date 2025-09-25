using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace McpCodeEditor.Services.Analysis;

/// <summary>
/// Service for analyzing TypeScript files using regex-based parsing and basic structural analysis
/// Provides symbol extraction and basic semantic analysis for TypeScript code
/// </summary>
public class TypeScriptAnalysisService(ILogger<TypeScriptAnalysisService> logger)
{
    // Regex patterns for TypeScript constructs
    private static readonly Regex FunctionPattern = new(@"(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(([^)]*)\)(?:\s*:\s*([^{;]+))?", RegexOptions.Compiled);
    private static readonly Regex ClassPattern = new(@"(?:export\s+)?(?:abstract\s+)?class\s+(\w+)(?:\s+extends\s+(\w+))?(?:\s+implements\s+([^{]+))?", RegexOptions.Compiled);
    private static readonly Regex InterfacePattern = new(@"(?:export\s+)?interface\s+(\w+)(?:\s+extends\s+([^{]+))?", RegexOptions.Compiled);
    private static readonly Regex MethodPattern = new(@"(?:public|private|protected)?\s*(?:static\s+)?(?:async\s+)?(\w+)\s*\(([^)]*)\)(?:\s*:\s*([^{;]+))?", RegexOptions.Compiled);
    private static readonly Regex VariablePattern = new(@"(?:export\s+)?(?:const|let|var)\s+(\w+)(?:\s*:\s*([^=;]+))?", RegexOptions.Compiled);
    private static readonly Regex TypeAliasPattern = new(@"(?:export\s+)?type\s+(\w+)\s*=\s*([^;]+)", RegexOptions.Compiled);
    private static readonly Regex ImportPattern = new("""import\s+(?:(?:{([^}]+)})|(?:(\w+))|(?:\*\s+as\s+(\w+)))\s+from\s+['"]([^'"]+)['"]""", RegexOptions.Compiled);
    private static readonly Regex ExportPattern = new("""export\s+(?:(?:default\s+)|(?:{([^}]+)})|(?:\*\s+from\s+['"]([^'"]+)['"]))""", RegexOptions.Compiled);

    /// <summary>
    /// Analyzes a TypeScript file and extracts structural information
    /// </summary>
    public async Task<TypeScriptAnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new TypeScriptAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}"
                };
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return await AnalyzeContentAsync(content, filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing TypeScript file: {FilePath}", filePath);
            return new TypeScriptAnalysisResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Analyzes TypeScript content string and extracts structural information
    /// </summary>
    public async Task<TypeScriptAnalysisResult> AnalyzeContentAsync(string content, string? filePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new TypeScriptAnalysisResult
            {
                Success = true,
                FilePath = filePath,
                ContentLength = content.Length
            };

            await Task.Run(() =>
            {
                // Extract symbols and structure using regex patterns
                ExtractFunctions(content, result);
                ExtractClasses(content, result);
                ExtractInterfaces(content, result);
                ExtractVariables(content, result);
                ExtractTypeAliases(content, result);
                ExtractImportsExports(content, result);
                
                // Basic syntax validation
                result.HasSyntaxErrors = CheckBasicSyntaxErrors(content);
                
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);

            logger.LogDebug("Successfully analyzed TypeScript content. Found {FunctionCount} functions, {ClassCount} classes, {InterfaceCount} interfaces",
                result.Functions.Count, result.Classes.Count, result.Interfaces.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing TypeScript content");
            return new TypeScriptAnalysisResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                FilePath = filePath
            };
        }
    }

    /// <summary>
    /// Extract function declarations from TypeScript content
    /// </summary>
    private static void ExtractFunctions(string content, TypeScriptAnalysisResult result)
    {
        var lines = content.Split('\n');
        var matches = FunctionPattern.Matches(content);

        foreach (Match match in matches)
        {
            var function = new TypeScriptFunction
            {
                Name = match.Groups[1].Value,
                Parameters = ParseParameters(match.Groups[2].Value),
                ReturnType = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null,
                IsExported = match.Value.Contains("export"),
                IsAsync = match.Value.Contains("async")
            };

            // Find line numbers
            var lineInfo = FindLineNumbers(content, match.Index, match.Length);
            function.StartLine = lineInfo.StartLine;
            function.EndLine = lineInfo.EndLine;
            function.StartColumn = lineInfo.StartColumn;
            function.EndColumn = lineInfo.EndColumn;

            result.Functions.Add(function);
        }
    }

    /// <summary>
    /// Extract class declarations from TypeScript content
    /// </summary>
    private static void ExtractClasses(string content, TypeScriptAnalysisResult result)
    {
        var matches = ClassPattern.Matches(content);

        foreach (Match match in matches)
        {
            var classInfo = new TypeScriptClass
            {
                Name = match.Groups[1].Value,
                ExtendsClass = match.Groups[2].Success ? match.Groups[2].Value : null,
                IsExported = match.Value.Contains("export"),
                IsAbstract = match.Value.Contains("abstract")
            };

            if (match.Groups[3].Success)
            {
                classInfo.ImplementsInterfaces = match.Groups[3].Value
                    .Split(',')
                    .Select(i => i.Trim())
                    .Where(i => !string.IsNullOrEmpty(i))
                    .ToList();
            }

            // Find line numbers
            var lineInfo = FindLineNumbers(content, match.Index, match.Length);
            classInfo.StartLine = lineInfo.StartLine;
            classInfo.EndLine = lineInfo.EndLine;
            classInfo.StartColumn = lineInfo.StartColumn;
            classInfo.EndColumn = lineInfo.EndColumn;

            result.Classes.Add(classInfo);
        }
    }

    /// <summary>
    /// Extract interface declarations from TypeScript content
    /// </summary>
    private static void ExtractInterfaces(string content, TypeScriptAnalysisResult result)
    {
        var matches = InterfacePattern.Matches(content);

        foreach (Match match in matches)
        {
            var interfaceInfo = new TypeScriptInterface
            {
                Name = match.Groups[1].Value,
                IsExported = match.Value.Contains("export")
            };

            if (match.Groups[2].Success)
            {
                interfaceInfo.ExtendsInterfaces = match.Groups[2].Value
                    .Split(',')
                    .Select(i => i.Trim())
                    .Where(i => !string.IsNullOrEmpty(i))
                    .ToList();
            }

            // Find line numbers
            var lineInfo = FindLineNumbers(content, match.Index, match.Length);
            interfaceInfo.StartLine = lineInfo.StartLine;
            interfaceInfo.EndLine = lineInfo.EndLine;
            interfaceInfo.StartColumn = lineInfo.StartColumn;
            interfaceInfo.EndColumn = lineInfo.EndColumn;

            result.Interfaces.Add(interfaceInfo);
        }
    }

    /// <summary>
    /// Extract variable declarations from TypeScript content
    /// </summary>
    private static void ExtractVariables(string content, TypeScriptAnalysisResult result)
    {
        var matches = VariablePattern.Matches(content);

        foreach (Match match in matches)
        {
            var variable = new TypeScriptVariable
            {
                Name = match.Groups[1].Value,
                Type = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null,
                IsExported = match.Value.Contains("export"),
                IsConst = match.Value.Contains("const"),
                IsLet = match.Value.Contains("let")
            };

            // Find line numbers
            var lineInfo = FindLineNumbers(content, match.Index, match.Length);
            variable.StartLine = lineInfo.StartLine;
            variable.EndLine = lineInfo.EndLine;

            result.Variables.Add(variable);
        }
    }

    /// <summary>
    /// Extract type alias declarations from TypeScript content
    /// </summary>
    private static void ExtractTypeAliases(string content, TypeScriptAnalysisResult result)
    {
        var matches = TypeAliasPattern.Matches(content);

        foreach (Match match in matches)
        {
            var typeAlias = new TypeScriptTypeAlias
            {
                Name = match.Groups[1].Value,
                AliasedType = match.Groups[2].Value.Trim(),
                IsExported = match.Value.Contains("export")
            };

            // Find line numbers
            var lineInfo = FindLineNumbers(content, match.Index, match.Length);
            typeAlias.StartLine = lineInfo.StartLine;
            typeAlias.EndLine = lineInfo.EndLine;

            result.TypeAliases.Add(typeAlias);
        }
    }

    /// <summary>
    /// Extract import and export statements from TypeScript content
    /// </summary>
    private static void ExtractImportsExports(string content, TypeScriptAnalysisResult result)
    {
        // Extract imports
        var importMatches = ImportPattern.Matches(content);
        foreach (Match match in importMatches)
        {
            var import = new TypeScriptImport
            {
                Source = match.Groups[4].Value,
                Line = FindLineNumber(content, match.Index)
            };

            if (match.Groups[1].Success) // Named imports
            {
                import.ImportedSymbols = match.Groups[1].Value
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            else if (match.Groups[2].Success) // Default import
            {
                import.ImportedSymbols = [match.Groups[2].Value];
                import.IsDefault = true;
            }
            else if (match.Groups[3].Success) // Namespace import
            {
                import.ImportedSymbols = [match.Groups[3].Value];
                import.IsNamespace = true;
            }

            result.Imports.Add(import);
        }

        // Extract exports
        var exportMatches = ExportPattern.Matches(content);
        foreach (Match match in exportMatches)
        {
            var export = new TypeScriptExport
            {
                Line = FindLineNumber(content, match.Index),
                IsDefault = match.Value.Contains("default")
            };

            if (match.Groups[1].Success) // Named exports
            {
                export.ExportedSymbols = match.Groups[1].Value
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            if (match.Groups[2].Success) // Re-exports
            {
                export.Source = match.Groups[2].Value;
            }

            result.Exports.Add(export);
        }
    }

    /// <summary>
    /// Parse function/method parameters from parameter string
    /// </summary>
    private static List<string> ParseParameters(string parametersString)
    {
        if (string.IsNullOrWhiteSpace(parametersString))
            return [];

        return parametersString
            .Split(',')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p.Split(':')[0].Trim()) // Get parameter name before type annotation
            .ToList();
    }

    /// <summary>
    /// Find line and column information for a match
    /// </summary>
    private static (int StartLine, int EndLine, int StartColumn, int EndColumn) FindLineNumbers(string content, int startIndex, int length)
    {
        var lines = content[..startIndex].Split('\n');
        var startLine = lines.Length;
        var startColumn = lines.LastOrDefault()?.Length ?? 0;

        var endContent = content[..(startIndex + length)];
        var endLines = endContent.Split('\n');
        var endLine = endLines.Length;
        var endColumn = endLines.LastOrDefault()?.Length ?? 0;

        return (startLine, endLine, startColumn, endColumn);
    }

    /// <summary>
    /// Find line number for a specific character index
    /// </summary>
    private static int FindLineNumber(string content, int index)
    {
        return content[..index].Split('\n').Length;
    }

    /// <summary>
    /// Perform basic syntax error checking
    /// </summary>
    private static bool CheckBasicSyntaxErrors(string content)
    {
        // Basic checks for common syntax errors
        var braceCount = content.Count(c => c == '{') - content.Count(c => c == '}');
        var parenCount = content.Count(c => c == '(') - content.Count(c => c == ')');
        var bracketCount = content.Count(c => c == '[') - content.Count(c => c == ']');

        return braceCount != 0 || parenCount != 0 || bracketCount != 0;
    }

    /// <summary>
    /// Get symbols by name - useful for refactoring operations
    /// </summary>
    public static List<TypeScriptSymbol> FindSymbolsByName(TypeScriptAnalysisResult analysisResult, string symbolName)
    {
        var symbols = new List<TypeScriptSymbol>();

        // Find in functions
        symbols.AddRange(analysisResult.Functions
            .Where(f => f.Name == symbolName)
            .Select(f => new TypeScriptSymbol
            {
                Name = f.Name,
                Type = "function",
                StartLine = f.StartLine,
                EndLine = f.EndLine,
                StartColumn = f.StartColumn,
                EndColumn = f.EndColumn
            }));

        // Find in classes
        symbols.AddRange(analysisResult.Classes
            .Where(c => c.Name == symbolName)
            .Select(c => new TypeScriptSymbol
            {
                Name = c.Name,
                Type = "class",
                StartLine = c.StartLine,
                EndLine = c.EndLine,
                StartColumn = c.StartColumn,
                EndColumn = c.EndColumn
            }));

        // Find in interfaces
        symbols.AddRange(analysisResult.Interfaces
            .Where(i => i.Name == symbolName)
            .Select(i => new TypeScriptSymbol
            {
                Name = i.Name,
                Type = "interface",
                StartLine = i.StartLine,
                EndLine = i.EndLine,
                StartColumn = i.StartColumn,
                EndColumn = i.EndColumn
            }));

        // Find in variables
        symbols.AddRange(analysisResult.Variables
            .Where(v => v.Name == symbolName)
            .Select(v => new TypeScriptSymbol
            {
                Name = v.Name,
                Type = "variable",
                StartLine = v.StartLine,
                EndLine = v.EndLine
            }));

        return symbols;
    }
}

/// <summary>
/// Result of TypeScript file analysis
/// </summary>
public class TypeScriptAnalysisResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FilePath { get; set; }
    public int ContentLength { get; set; }
    public bool HasSyntaxErrors { get; set; }
    
    public List<TypeScriptFunction> Functions { get; set; } = [];
    public List<TypeScriptClass> Classes { get; set; } = [];
    public List<TypeScriptInterface> Interfaces { get; set; } = [];
    public List<TypeScriptVariable> Variables { get; set; } = [];
    public List<TypeScriptTypeAlias> TypeAliases { get; set; } = [];
    public List<TypeScriptImport> Imports { get; set; } = [];
    public List<TypeScriptExport> Exports { get; set; } = [];
}

/// <summary>
/// TypeScript function information
/// </summary>
public class TypeScriptFunction
{
    public string Name { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
    public List<string> Parameters { get; set; } = [];
    public string? ReturnType { get; set; }
    public bool IsAsync { get; set; }
    public bool IsExported { get; set; }
}

/// <summary>
/// TypeScript class information
/// </summary>
public class TypeScriptClass
{
    public string Name { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
    public string? ExtendsClass { get; set; }
    public List<string> ImplementsInterfaces { get; set; } = [];
    public bool IsExported { get; set; }
    public bool IsAbstract { get; set; }
}

/// <summary>
/// TypeScript interface information
/// </summary>
public class TypeScriptInterface
{
    public string Name { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
    public List<string> ExtendsInterfaces { get; set; } = [];
    public bool IsExported { get; set; }
}

/// <summary>
/// TypeScript variable information
/// </summary>
public class TypeScriptVariable
{
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string? InitializerType { get; set; }
    public bool IsConst { get; set; }
    public bool IsLet { get; set; }
    public bool IsExported { get; set; }
}

/// <summary>
/// TypeScript type alias information
/// </summary>
public class TypeScriptTypeAlias
{
    public string Name { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string? AliasedType { get; set; }
    public bool IsExported { get; set; }
}

/// <summary>
/// TypeScript import information
/// </summary>
public class TypeScriptImport
{
    public string Source { get; set; } = string.Empty;
    public List<string> ImportedSymbols { get; set; } = [];
    public int Line { get; set; }
    public bool IsDefault { get; set; }
    public bool IsNamespace { get; set; }
    public string? Alias { get; set; }
}

/// <summary>
/// TypeScript export information
/// </summary>
public class TypeScriptExport
{
    public int Line { get; set; }
    public bool IsDefault { get; set; }
    public List<string> ExportedSymbols { get; set; } = [];
    public string? Source { get; set; }
}

/// <summary>
/// General TypeScript symbol for refactoring operations
/// </summary>
public class TypeScriptSymbol
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "function", "class", "interface", "variable", etc.
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
}
