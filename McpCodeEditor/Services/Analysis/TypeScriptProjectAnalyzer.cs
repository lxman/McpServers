using System.Text.RegularExpressions;
using McpCodeEditor.Services.TypeScript;

namespace McpCodeEditor.Services.Analysis;

/// <summary>
/// Analyzes TypeScript project structure and cross-file dependencies for advanced refactoring operations
/// </summary>
public class TypeScriptProjectAnalyzer(
    TypeScriptAnalysisService analysisService,
    TypeScriptFileResolver fileResolver,
    CodeEditorConfigurationService config)
{
    /// <summary>
    /// Represents a cross-file symbol reference
    /// </summary>
    public class CrossFileReference
    {
        public string SourceFile { get; set; } = string.Empty;
        public string TargetFile { get; set; } = string.Empty;
        public string SymbolName { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty; // import, export, usage
        public int LineNumber { get; set; }
        public int Column { get; set; }
        public string Context { get; set; } = string.Empty; // surrounding code context
    }

    /// <summary>
    /// Represents a TypeScript symbol with its dependencies
    /// </summary>
    public class ProjectSymbolInfo
    {
        public string SymbolName { get; set; } = string.Empty;
        public string DefinitionFile { get; set; } = string.Empty;
        public string SymbolType { get; set; } = string.Empty; // class, function, interface, etc.
        public int DefinitionLine { get; set; }
        public bool IsExported { get; set; }
        public List<CrossFileReference> References { get; set; } = [];
        public List<string> ImportedBy { get; set; } = [];
        public List<string> Exports { get; set; } = [];
    }

    /// <summary>
    /// Project analysis result containing cross-file dependencies
    /// </summary>
    public class ProjectAnalysisResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public Dictionary<string, ProjectSymbolInfo> Symbols { get; set; } = new();
        public List<CrossFileReference> CrossFileReferences { get; set; } = [];
        public Dictionary<string, List<string>> FileDependencies { get; set; } = new();
        public List<string> ProjectFiles { get; set; } = [];
    }

    /// <summary>
    /// Analyze TypeScript project for cross-file symbol dependencies
    /// </summary>
    public async Task<ProjectAnalysisResult> AnalyzeProjectAsync(
        string? rootPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new ProjectAnalysisResult { Success = true };
            string projectRoot = rootPath ?? config.DefaultWorkspace;

            // Discover TypeScript files in the project
            TypeScriptFileDiscoveryResult fileDiscoveryResult = await fileResolver.FindTypeScriptFilesAsync(projectRoot);
            if (!fileDiscoveryResult.Success)
            {
                return new ProjectAnalysisResult
                {
                    Success = false,
                    Error = fileDiscoveryResult.ErrorMessage ?? "Failed to discover TypeScript files"
                };
            }

            result.ProjectFiles = fileDiscoveryResult.SourceFiles;

            // Analyze each file and build symbol map
            foreach (string filePath in fileDiscoveryResult.SourceFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await AnalyzeFileForSymbolsAsync(filePath, result, cancellationToken);
            }

            // Build cross-file reference map
            await BuildCrossFileReferencesAsync(result, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            return new ProjectAnalysisResult
            {
                Success = false,
                Error = $"Project analysis failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Find all cross-file references for a specific symbol
    /// </summary>
    public async Task<List<CrossFileReference>> FindSymbolReferencesAsync(
        string symbolName,
        string? rootPath = null,
        CancellationToken cancellationToken = default)
    {
        ProjectAnalysisResult projectAnalysis = await AnalyzeProjectAsync(rootPath, cancellationToken);
        if (!projectAnalysis.Success)
            return [];

        var references = new List<CrossFileReference>();

        // Find direct references
        references.AddRange(projectAnalysis.CrossFileReferences
            .Where(r => r.SymbolName == symbolName));

        // Find symbol in project symbols and get its references
        if (projectAnalysis.Symbols.TryGetValue(symbolName, out ProjectSymbolInfo? symbolInfo))
        {
            references.AddRange(symbolInfo.References);
        }

        return references.DistinctBy(r => $"{r.SourceFile}:{r.LineNumber}:{r.Column}").ToList();
    }

    /// <summary>
    /// Analyze a single file for symbols and exports
    /// </summary>
    private async Task AnalyzeFileForSymbolsAsync(
        string filePath,
        ProjectAnalysisResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            TypeScriptAnalysisResult fileAnalysis = await analysisService.AnalyzeFileAsync(filePath, cancellationToken);
            if (!fileAnalysis.Success)
                return;

            string content = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            // Extract symbols with export information
            foreach (TypeScriptClass symbol in fileAnalysis.Classes)
            {
                var symbolInfo = new ProjectSymbolInfo
                {
                    SymbolName = symbol.Name,
                    DefinitionFile = filePath,
                    SymbolType = "class",
                    DefinitionLine = symbol.StartLine,
                    IsExported = IsSymbolExported(content, symbol.Name, symbol.StartLine)
                };

                result.Symbols[symbol.Name] = symbolInfo;
            }

            foreach (TypeScriptFunction symbol in fileAnalysis.Functions)
            {
                var symbolInfo = new ProjectSymbolInfo
                {
                    SymbolName = symbol.Name,
                    DefinitionFile = filePath,
                    SymbolType = "function",
                    DefinitionLine = symbol.StartLine,
                    IsExported = IsSymbolExported(content, symbol.Name, symbol.StartLine)
                };

                result.Symbols[symbol.Name] = symbolInfo;
            }

            foreach (TypeScriptInterface symbol in fileAnalysis.Interfaces)
            {
                var symbolInfo = new ProjectSymbolInfo
                {
                    SymbolName = symbol.Name,
                    DefinitionFile = filePath,
                    SymbolType = "interface",
                    DefinitionLine = symbol.StartLine,
                    IsExported = IsSymbolExported(content, symbol.Name, symbol.StartLine)
                };

                result.Symbols[symbol.Name] = symbolInfo;
            }

            // Analyze imports and exports in this file
            await AnalyzeImportsAndExportsAsync(filePath, content, result);
        }
        catch (Exception)
        {
            // Skip files that can't be analyzed
        }
    }

    /// <summary>
    /// Build cross-file reference map by analyzing imports and usage
    /// </summary>
    private static async Task BuildCrossFileReferencesAsync(
        ProjectAnalysisResult result,
        CancellationToken cancellationToken)
    {
        foreach (string filePath in result.ProjectFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                string content = await File.ReadAllTextAsync(filePath, cancellationToken);
                await AnalyzeFileReferencesAsync(filePath, content, result);
            }
            catch (Exception)
            {
                // Skip files that can't be read
            }
        }
    }

    /// <summary>
    /// Analyze imports and exports in a file
    /// </summary>
    private async Task AnalyzeImportsAndExportsAsync(
        string filePath,
        string content,
        ProjectAnalysisResult result)
    {
        await Task.CompletedTask; // Make async for consistency

        // Find import statements
        var importPattern = @"import\s*\{([^}]+)\}\s*from\s*['""]([^'""]+)['""]";
        MatchCollection importMatches = Regex.Matches(content, importPattern, RegexOptions.Multiline);

        foreach (Match match in importMatches)
        {
            string importedSymbols = match.Groups[1].Value;
            string importPath = match.Groups[2].Value;
            
            IEnumerable<string> symbols = importedSymbols.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));

            foreach (string symbol in symbols)
            {
                var reference = new CrossFileReference
                {
                    SourceFile = filePath,
                    TargetFile = ResolveImportPath(importPath, filePath),
                    SymbolName = symbol,
                    ReferenceType = "import",
                    LineNumber = GetLineNumber(content, match.Index),
                    Column = GetColumnNumber(content, match.Index),
                    Context = match.Value
                };

                result.CrossFileReferences.Add(reference);

                // Add to symbol's imported by list
                if (result.Symbols.ContainsKey(symbol))
                {
                    result.Symbols[symbol].ImportedBy.Add(filePath);
                }
            }
        }

        // Find export statements
        var exportPattern = @"export\s*\{([^}]+)\}";
        MatchCollection exportMatches = Regex.Matches(content, exportPattern, RegexOptions.Multiline);

        foreach (Match match in exportMatches)
        {
            string exportedSymbols = match.Groups[1].Value;
            IEnumerable<string> symbols = exportedSymbols.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));

            foreach (string symbol in symbols)
            {
                if (result.Symbols.ContainsKey(symbol))
                {
                    result.Symbols[symbol].Exports.Add(filePath);
                    result.Symbols[symbol].IsExported = true;
                }
            }
        }
    }

    /// <summary>
    /// Analyze symbol references within a file
    /// </summary>
    private static async Task AnalyzeFileReferencesAsync(
        string filePath,
        string content,
        ProjectAnalysisResult result)
    {
        await Task.CompletedTask; // Make async for consistency

        // For each known symbol, find its usage in this file
        foreach (ProjectSymbolInfo symbolInfo in result.Symbols.Values)
        {
            if (symbolInfo.DefinitionFile == filePath)
                continue; // Skip self-references

            // Find symbol usage patterns
            var usagePatterns = new[]
            {
                $@"\b{Regex.Escape(symbolInfo.SymbolName)}\s*\(",  // Function calls
                $@"\b{Regex.Escape(symbolInfo.SymbolName)}\b",     // General references
                $@"new\s+{Regex.Escape(symbolInfo.SymbolName)}\b", // Constructor calls
                $@":\s*{Regex.Escape(symbolInfo.SymbolName)}\b"    // Type annotations
            };

            foreach (string pattern in usagePatterns)
            {
                MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    var reference = new CrossFileReference
                    {
                        SourceFile = filePath,
                        TargetFile = symbolInfo.DefinitionFile,
                        SymbolName = symbolInfo.SymbolName,
                        ReferenceType = "usage",
                        LineNumber = GetLineNumber(content, match.Index),
                        Column = GetColumnNumber(content, match.Index),
                        Context = GetContextAroundMatch(content, match.Index)
                    };

                    symbolInfo.References.Add(reference);
                }
            }
        }
    }

    /// <summary>
    /// Check if a symbol is exported from the file
    /// </summary>
    private static bool IsSymbolExported(string content, string symbolName, int definitionLine)
    {
        // Check for export keyword on the same line or nearby
        string[] lines = content.Split('\n');
        if (definitionLine > 0 && definitionLine <= lines.Length)
        {
            string definitionLineContent = lines[definitionLine - 1];
            
            // Check if export keyword is on the definition line
            if (definitionLineContent.Contains("export"))
                return true;

            // Check if symbol is exported in a separate export statement
            var exportPattern = $@"export\s*\{{[^}}]*\b{Regex.Escape(symbolName)}\b[^}}]*\}}";
            return Regex.IsMatch(content, exportPattern, RegexOptions.Multiline);
        }

        return false;
    }

    /// <summary>
    /// Resolve import path to absolute file path
    /// </summary>
    private string ResolveImportPath(string importPath, string currentFile)
    {
        try
        {
            if (importPath.StartsWith("./") || importPath.StartsWith("../"))
            {
                // Relative path
                string currentDir = Path.GetDirectoryName(currentFile) ?? string.Empty;
                string resolvedPath = Path.Combine(currentDir, importPath);
                
                // Add .ts extension if not present
                if (!Path.HasExtension(resolvedPath))
                {
                    resolvedPath += ".ts";
                }
                
                return Path.GetFullPath(resolvedPath);
            }
            else
            {
                // Absolute or package import - try to resolve within project
                string projectRoot = config.DefaultWorkspace;
                string possiblePath = Path.Combine(projectRoot, importPath + ".ts");
                
                if (File.Exists(possiblePath))
                    return possiblePath;
                
                return importPath; // Return as-is if can't resolve
            }
        }
        catch
        {
            return importPath; // Return as-is if resolution fails
        }
    }

    /// <summary>
    /// Get line number from character index
    /// </summary>
    private static int GetLineNumber(string content, int index)
    {
        return content.Take(index).Count(c => c == '\n') + 1;
    }

    /// <summary>
    /// Get column number from character index
    /// </summary>
    private static int GetColumnNumber(string content, int index)
    {
        int lastNewLine = content.LastIndexOf('\n', index);
        return index - lastNewLine;
    }

    /// <summary>
    /// Get context around a match for better understanding
    /// </summary>
    private static string GetContextAroundMatch(string content, int index, int contextLength = 50)
    {
        int start = Math.Max(0, index - contextLength);
        int end = Math.Min(content.Length, index + contextLength);
        return content.Substring(start, end - start).Trim();
    }
}
