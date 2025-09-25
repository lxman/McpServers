using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace McpCodeEditor.Services;

public class CodeAnalysisService(CodeEditorConfigurationService config)
{
    public async Task<object> AnalyzeAsync(string? path, string? content, string? language,
        bool includeDiagnostics, bool includeSymbols, bool includeMetrics)
    {
        try
        {
            // Get content either from path or direct content
            string sourceCode;
            string actualPath;

            if (!string.IsNullOrEmpty(path))
            {
                actualPath = Path.GetFullPath(path);
                if (!File.Exists(actualPath))
                {
                    return new { success = false, error = $"File not found: {path}" };
                }
                sourceCode = await File.ReadAllTextAsync(actualPath);
            }
            else if (!string.IsNullOrEmpty(content))
            {
                sourceCode = content;
                actualPath = "memory://untitled";
            }
            else
            {
                return new { success = false, error = "Either path or content must be provided" };
            }

            // Detect language if not provided
            if (string.IsNullOrEmpty(language))
            {
                language = DetectLanguage(actualPath, sourceCode);
            }

            var fileInfo = new FileInfo(actualPath);
            if (fileInfo.Exists && fileInfo.Length > config.CodeAnalysis.MaxAnalysisFileSize)
            {
                return new { success = false, error = $"File too large for analysis: {fileInfo.Length} bytes" };
            }

            var result = new
            {
                success = true,
                path = actualPath,
                language = language,
                size = sourceCode.Length,
                line_count = sourceCode.Split('\n').Length,
                analysis = language?.ToLowerInvariant() switch
                {
                    "csharp" or "cs" => await AnalyzeCSharpAsync(sourceCode, includeDiagnostics, includeSymbols, includeMetrics),
                    _ => await AnalyzeGenericAsync(sourceCode, language ?? "unknown", includeDiagnostics, includeSymbols, includeMetrics)
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    public static async Task<object> FormatAsync(string? path, string? content, string? language, bool writeToFile)
    {
        try
        {
            // Get content either from path or direct content
            string sourceCode;
            string actualPath;

            if (!string.IsNullOrEmpty(path))
            {
                actualPath = Path.GetFullPath(path);
                if (!File.Exists(actualPath))
                {
                    return new { success = false, error = $"File not found: {path}" };
                }
                sourceCode = await File.ReadAllTextAsync(actualPath);
            }
            else if (!string.IsNullOrEmpty(content))
            {
                sourceCode = content;
                actualPath = "memory://untitled";
            }
            else
            {
                return new { success = false, error = "Either path or content must be provided" };
            }

            // Detect language if not provided
            if (string.IsNullOrEmpty(language))
            {
                language = DetectLanguage(actualPath, sourceCode);
            }

            var formattedCode = language?.ToLowerInvariant() switch
            {
                "csharp" or "cs" => await FormatCSharpAsync(sourceCode),
                _ => sourceCode // For now, only C# formatting is implemented
            };

            // Write back to file if requested and path is provided
            if (writeToFile && !string.IsNullOrEmpty(path) && File.Exists(actualPath))
            {
                await File.WriteAllTextAsync(actualPath, formattedCode);
            }

            return new
            {
                success = true,
                path = actualPath,
                language = language,
                original_size = sourceCode.Length,
                formatted_size = formattedCode.Length,
                formatted_content = formattedCode,
                written_to_file = writeToFile && !string.IsNullOrEmpty(path)
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object> AnalyzeCSharpAsync(string sourceCode, bool includeDiagnostics, bool includeSymbols, bool includeMetrics)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = await syntaxTree.GetRootAsync();

            var result = new
            {
                syntax_valid = !syntaxTree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
                diagnostics = includeDiagnostics ? GetDiagnostics(syntaxTree) : null,
                symbols = includeSymbols ? await GetSymbolsAsync(syntaxTree) : null,
                metrics = includeMetrics ? GetCodeMetrics(root, sourceCode) : null,
                structure = new
                {
                    namespaces = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax>().Count(),
                    classes = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Count(),
                    interfaces = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax>().Count(),
                    methods = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Count(),
                    properties = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>().Count()
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            return new { error = $"C# analysis failed: {ex.Message}" };
        }
    }

    private static async Task<object> AnalyzeGenericAsync(string sourceCode, string language, bool includeDiagnostics, bool includeSymbols, bool includeMetrics)
    {
        // For non-C# languages, provide basic analysis
        var lines = sourceCode.Split('\n');
        var nonEmptyLines = lines.Count(line => !string.IsNullOrWhiteSpace(line));
        var commentLines = language.ToLowerInvariant() switch
        {
            "javascript" or "js" or "typescript" or "ts" => lines.Count(line => line.TrimStart().StartsWith("//")),
            "python" or "py" => lines.Count(line => line.TrimStart().StartsWith("#")),
            "java" => lines.Count(line => line.TrimStart().StartsWith("//")),
            _ => 0
        };

        return new
        {
            language_support = "basic",
            line_count = lines.Length,
            non_empty_lines = nonEmptyLines,
            comment_lines = commentLines,
            estimated_complexity = EstimateComplexity(sourceCode)
        };
    }

    private static async Task<string> FormatCSharpAsync(string sourceCode)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = await syntaxTree.GetRootAsync();

            var workspace = new AdhocWorkspace();
            var formattedRoot = Formatter.Format(root, workspace);

            return formattedRoot.ToFullString();
        }
        catch (Exception)
        {
            // If formatting fails, return original code
            return sourceCode;
        }
    }

    private static string DetectLanguage(string path, string content)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".py" => "python",
            ".java" => "java",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" => "c",
            ".h" or ".hpp" => "header",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".json" => "json",
            ".xml" => "xml",
            ".yaml" or ".yml" => "yaml",
            ".sql" => "sql",
            ".sh" => "bash",
            ".ps1" => "powershell",
            ".md" => "markdown",
            _ => "text"
        };
    }

    private object GetDiagnostics(SyntaxTree syntaxTree)
    {
        var diagnostics = syntaxTree.GetDiagnostics();

        return new
        {
            total_count = diagnostics.Count(),
            errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(FormatDiagnostic),
            warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(FormatDiagnostic),
            info = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Info).Select(FormatDiagnostic)
        };
    }

    private object FormatDiagnostic(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        return new
        {
            id = diagnostic.Id,
            severity = diagnostic.Severity.ToString(),
            message = diagnostic.GetMessage(),
            line = lineSpan.StartLinePosition.Line + 1,
            column = lineSpan.StartLinePosition.Character + 1,
            end_line = lineSpan.EndLinePosition.Line + 1,
            end_column = lineSpan.EndLinePosition.Character + 1
        };
    }

    private static async Task<object> GetSymbolsAsync(SyntaxTree syntaxTree)
    {
        try
        {
            var root = await syntaxTree.GetRootAsync();

            return new
            {
                classes = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                    .Select(c => new
                    {
                        name = c.Identifier.ValueText,
                        line = c.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        modifiers = c.Modifiers.Select(m => m.ValueText).ToArray()
                    }),
                methods = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                    .Select(m => new
                    {
                        name = m.Identifier.ValueText,
                        line = m.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        return_type = m.ReturnType.ToString(),
                        parameters = m.ParameterList.Parameters.Select(p => new
                        {
                            name = p.Identifier.ValueText,
                            type = p.Type?.ToString()
                        }).ToArray()
                    }),
                properties = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
                    .Select(p => new
                    {
                        name = p.Identifier.ValueText,
                        line = p.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        type = p.Type.ToString(),
                        has_getter = p.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.GetKeyword)) ?? false,
                        has_setter = p.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.SetKeyword)) ?? false
                    })
            };
        }
        catch (Exception)
        {
            return new { error = "Failed to extract symbols" };
        }
    }

    private static object GetCodeMetrics(SyntaxNode root, string sourceCode)
    {
        var lines = sourceCode.Split('\n');
        var nonEmptyLines = lines.Count(line => !string.IsNullOrWhiteSpace(line));
        var commentLines = lines.Count(line => line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("/*"));

        return new
        {
            total_lines = lines.Length,
            code_lines = nonEmptyLines,
            comment_lines = commentLines,
            blank_lines = lines.Length - nonEmptyLines,
            cyclomatic_complexity = CalculateCyclomaticComplexity(root),
            maintainability_index = CalculateMaintainabilityIndex(nonEmptyLines, commentLines, CalculateCyclomaticComplexity(root))
        };
    }

    private static int CalculateCyclomaticComplexity(SyntaxNode root)
    {
        // Basic cyclomatic complexity calculation
        // Start with 1 and add 1 for each decision point
        var complexity = 1;

        complexity += root.DescendantNodes().Count(n =>
            n.IsKind(SyntaxKind.IfStatement) ||
            n.IsKind(SyntaxKind.WhileStatement) ||
            n.IsKind(SyntaxKind.ForStatement) ||
            n.IsKind(SyntaxKind.ForEachStatement) ||
            n.IsKind(SyntaxKind.SwitchStatement) ||
            n.IsKind(SyntaxKind.CaseSwitchLabel) ||
            n.IsKind(SyntaxKind.ConditionalExpression) ||
            n.IsKind(SyntaxKind.LogicalAndExpression) ||
            n.IsKind(SyntaxKind.LogicalOrExpression) ||
            n.IsKind(SyntaxKind.CatchClause));

        return complexity;
    }

    private static int CalculateMaintainabilityIndex(int codeLines, int commentLines, int complexity)
    {
        // Simplified maintainability index calculation
        // Real formula is more complex and requires Halstead metrics
        var volume = Math.Log(codeLines + 1) * 16.2;
        var commentRatio = codeLines > 0 ? (double)commentLines / codeLines : 0;
        var complexityFactor = Math.Log(complexity) * 5.2;

        var index = Math.Max(0, (171 - complexityFactor - volume + (commentRatio * 50)) * 100 / 171);
        return (int)Math.Round(index);
    }

    private static string EstimateComplexity(string sourceCode)
    {
        var lines = sourceCode.Split('\n');
        var keywords = new[] { "if", "else", "while", "for", "switch", "case", "try", "catch" };
        var complexityScore = 0;

        foreach (var line in lines)
        {
            foreach (var keyword in keywords)
            {
                if (line.Contains(keyword))
                {
                    complexityScore++;
                }
            }
        }

        return complexityScore switch
        {
            < 5 => "low",
            < 15 => "medium",
            < 30 => "high",
            _ => "very_high"
        };
    }
}
