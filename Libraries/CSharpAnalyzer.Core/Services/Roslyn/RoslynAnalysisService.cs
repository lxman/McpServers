using System.Collections.Immutable;
using System.Reflection;
using CSharpAnalyzer.Core.Models.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using SymbolInfo = CSharpAnalyzer.Core.Models.Roslyn.SymbolInfo;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;
using RoslynSymbolInfo = Microsoft.CodeAnalysis.SymbolInfo;

namespace CSharpAnalyzer.Core.Services.Roslyn;

public class RoslynAnalysisService
{
    public static AnalyzeCodeResponse AnalyzeCodeAsync(string code, string? filePath = null)
    {
        var response = new AnalyzeCodeResponse { Success = true };

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            
            // Create a compilation to get semantic diagnostics
            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                [syntaxTree],
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();
            
            foreach (Diagnostic diagnostic in diagnostics)
            {
                FileLinePositionSpan lineSpan = diagnostic.Location.GetLineSpan();
                
                response.Diagnostics.Add(new DiagnosticInfo
                {
                    Id = diagnostic.Id,
                    Severity = diagnostic.Severity.ToString(),
                    Message = diagnostic.GetMessage(),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    EndLine = lineSpan.EndLinePosition.Line + 1,
                    EndColumn = lineSpan.EndLinePosition.Character + 1,
                    FilePath = lineSpan.Path
                });

                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        response.ErrorCount++;
                        break;
                    case DiagnosticSeverity.Warning:
                        response.WarningCount++;
                        break;
                    case DiagnosticSeverity.Info:
                        response.InfoCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown diagnostic severity {diagnostic.Severity}");
                }
            }
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Diagnostics.Add(new DiagnosticInfo
            {
                Id = "ANALYSIS_ERROR",
                Severity = "Error",
                Message = $"Analysis failed: {ex.Message}",
                Line = 0,
                Column = 0
            });
        }

        return response;
    }

    public static async Task<GetSymbolsResponse> GetSymbolsAsync(string code, string? filePath = null, string? filter = null)
    {
        var response = new GetSymbolsResponse();

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            SyntaxNode root = await syntaxTree.GetRootAsync();

            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                [syntaxTree],
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find all member declarations
            List<SyntaxNode> members = root.DescendantNodes()
                .Where(n => n is MemberDeclarationSyntax)
                .ToList();

            foreach (SyntaxNode member in members)
            {
                ISymbol? symbol = semanticModel.GetDeclaredSymbol(member);
                if (symbol == null) continue;

                // Apply filter if specified
                if (!string.IsNullOrEmpty(filter))
                {
                    string symbolKind = GetSymbolKind(symbol);
                    if (!symbolKind.Equals(filter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                FileLinePositionSpan location = member.GetLocation().GetLineSpan();

                response.Symbols.Add(new SymbolInfo
                {
                    Name = symbol.Name,
                    Kind = GetSymbolKind(symbol),
                    Type = GetSymbolType(symbol),
                    ContainingType = symbol.ContainingType?.Name ?? string.Empty,
                    Accessibility = symbol.DeclaredAccessibility.ToString(),
                    IsStatic = symbol.IsStatic,
                    IsAbstract = symbol.IsAbstract,
                    IsVirtual = symbol.IsVirtual,
                    Line = location.StartLinePosition.Line + 1,
                    Column = location.StartLinePosition.Character + 1
                });
            }

            response.TotalCount = response.Symbols.Count;
        }
        catch (Exception ex)
        {
            // Return the empty response with error logged
            await Console.Error.WriteLineAsync($"GetSymbols failed: {ex.Message}");
        }

        return response;
    }

    public static async Task<FormatCodeResponse> FormatCodeAsync(string code, string? filePath = null)
    {
        var response = new FormatCodeResponse { Success = true, OriginalCode = code };

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            SyntaxNode root = await syntaxTree.GetRootAsync();

            // Format using Roslyn's formatting API
            var workspace = new AdhocWorkspace();
            SyntaxNode formattedRoot = Formatter.Format(
                root, 
                workspace, 
                workspace.Options);

            response.FormattedCode = formattedRoot.ToFullString();
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.FormattedCode = code;
            response.Error = $"Formatting failed: {ex.Message}";
        }

        return response;
    }

    public static async Task<GetTypeInfoResponse> GetTypeInfoAsync(string code, int line, int column, string? filePath = null)
    {
        var response = new GetTypeInfoResponse { Success = true };

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            SyntaxNode root = await syntaxTree.GetRootAsync();

            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                [syntaxTree],
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Convert line/column to position (0-based)
            int position = syntaxTree.GetText().Lines[line - 1].Start + (column - 1);
            SyntaxNode? node = root.FindToken(position).Parent;

            if (node != null)
            {
                RoslynSymbolInfo symbolInfo = semanticModel.GetSymbolInfo(node);
                TypeInfo typeInfo = semanticModel.GetTypeInfo(node);

                response.TypeName = typeInfo.Type?.ToDisplayString() ?? "Unknown";
                response.SymbolName = symbolInfo.Symbol?.Name ?? string.Empty;
                response.SymbolKind = symbolInfo.Symbol?.Kind.ToString() ?? string.Empty;
                response.ContainingType = symbolInfo.Symbol?.ContainingType?.ToDisplayString() ?? string.Empty;

                // Get documentation if available
                string? documentation = symbolInfo.Symbol?.GetDocumentationCommentXml();
                if (!string.IsNullOrEmpty(documentation))
                {
                    response.Documentation = documentation;
                }
            }
            else
            {
                response.Success = false;
                response.Error = "No symbol found at specified position";
            }
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = $"Type info retrieval failed: {ex.Message}";
        }

        return response;
    }

    public static async Task<CalculateMetricsResponse> CalculateMetricsAsync(string code, string? filePath = null)
    {
        var response = new CalculateMetricsResponse { Success = true };

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            SyntaxNode root = await syntaxTree.GetRootAsync();

            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                [syntaxTree],
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Calculate basic metrics
            response.TotalLines = code.Split('\n').Length;
            response.TotalClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
            response.TotalMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count();
            response.TotalProperties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Count();

            // Calculate cyclomatic complexity for each method
            IEnumerable<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            var complexities = new List<int>();

            foreach (MethodDeclarationSyntax method in methods)
            {
                int complexity = CalculateCyclomaticComplexity(method);
                complexities.Add(complexity);
            }

            response.AverageCyclomaticComplexity = complexities.Any() 
                ? complexities.Average() 
                : 0;
            response.MaxCyclomaticComplexity = complexities.Any() 
                ? complexities.Max() 
                : 0;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = $"Metrics calculation failed: {ex.Message}";
        }

        return response;
    }

    private static int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
    {
        // Start with 1 for the method itself
        var complexity = 1;

        // Add 1 for each decision point
        IEnumerable<SyntaxNode> decisionPoints = method.DescendantNodes().Where(node =>
            node is IfStatementSyntax ||
            node is WhileStatementSyntax ||
            node is ForStatementSyntax ||
            node is ForEachStatementSyntax ||
            node is CaseSwitchLabelSyntax ||
            node is CatchClauseSyntax ||
            node is ConditionalExpressionSyntax ||
            node is BinaryExpressionSyntax binary && (
                binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                binary.IsKind(SyntaxKind.LogicalOrExpression))
        );

        complexity += decisionPoints.Count();
        return complexity;
    }

    private static string GetSymbolKind(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.NamedType => ((INamedTypeSymbol)symbol).TypeKind.ToString().ToLower(),
            SymbolKind.Method => "method",
            SymbolKind.Property => "property",
            SymbolKind.Field => "field",
            SymbolKind.Event => "event",
            _ => symbol.Kind.ToString().ToLower()
        };
    }

    private static string GetSymbolType(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.ReturnType.ToDisplayString(),
            IPropertySymbol property => property.Type.ToDisplayString(),
            IFieldSymbol field => field.Type.ToDisplayString(),
            IEventSymbol @event => @event.Type.ToDisplayString(),
            INamedTypeSymbol type => type.ToDisplayString(),
            _ => string.Empty
        };
    }

    private static IEnumerable<MetadataReference> GetBasicReferences()
    {
        Assembly[] assemblies =
        [
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Enumerable).Assembly,
            typeof(List<>).Assembly
        ];

        return assemblies
            .Where(a => !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
    }

    public static async Task<RemoveUnusedUsingsResponse> RemoveUnusedUsingsAsync(string code, string? filePath = null)
    {
        var response = new RemoveUnusedUsingsResponse { Success = true };

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            SyntaxNode root = await syntaxTree.GetRootAsync();

            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                [syntaxTree],
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Get all using directives
            List<UsingDirectiveSyntax> usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
            var unusedUsings = new List<UsingDirectiveSyntax>();

            foreach (UsingDirectiveSyntax usingDirective in usings)
            {
                var usingName = usingDirective.Name?.ToString();

                if (string.IsNullOrEmpty(usingName))
                    continue;

                // Check if any symbols from this namespace are used
                IEnumerable<SyntaxNode> allNodes = root.DescendantNodes().Where(n => n != usingDirective);

                bool isUsed = allNodes
                    .Select(node => semanticModel.GetSymbolInfo(node))
                    .Select(symbolInfo => symbolInfo.Symbol?.ContainingNamespace?.ToDisplayString())
                    .OfType<string>()
                    .Any(symbolNamespace => (symbolNamespace == usingName || symbolNamespace.StartsWith(usingName + ".")));

                if (isUsed) continue;
                unusedUsings.Add(usingDirective);
                response.RemovedUsings.Add(usingName);
            }

            // Remove unused usings
            root = root.RemoveNodes(unusedUsings, SyntaxRemoveOptions.KeepNoTrivia)!;
            response.CleanedCode = root.ToFullString();
            response.RemovedCount = unusedUsings.Count;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = $"Failed to remove unused usings: {ex.Message}";
            response.CleanedCode = code;
        }

        return response;
    }

    public static async Task<FindDeadCodeResponse> FindDeadCodeAsync(string code, string? filePath = null)
    {
        var response = new FindDeadCodeResponse { Success = true };

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            SyntaxNode root = await syntaxTree.GetRootAsync();

            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                [syntaxTree],
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Find unreachable code from diagnostics
            List<Diagnostic> diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Id == "CS0162") // Unreachable code detected
                .ToList();

            foreach (Diagnostic diagnostic in diagnostics)
            {
                FileLinePositionSpan lineSpan = diagnostic.Location.GetLineSpan();
                response.DeadCode.Add(new DeadCodeInfo
                {
                    Kind = "UnreachableCode",
                    Name = "Unreachable code",
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Message = diagnostic.GetMessage(),
                    Suggestion = "Remove this unreachable code"
                });
            }

            // Find unused private members
            List<SyntaxNode> privateMembers = root.DescendantNodes()
                .Where(n => n is MethodDeclarationSyntax || n is FieldDeclarationSyntax || n is PropertyDeclarationSyntax)
                .ToList();

            foreach (SyntaxNode member in privateMembers)
            {
                bool isPrivate = member.ChildTokens().Any(t => t.IsKind(SyntaxKind.PrivateKeyword)) ||
                                 !member.ChildTokens().Any(t => 
                                     t.IsKind(SyntaxKind.PublicKeyword) ||
                                     t.IsKind(SyntaxKind.ProtectedKeyword) ||
                                     t.IsKind(SyntaxKind.InternalKeyword));

                if (!isPrivate)
                    continue;

                ISymbol? symbol = semanticModel.GetDeclaredSymbol(member);
                if (symbol == null)
                    continue;

                // Simple check if the symbol is referenced anywhere
                bool hasReferences = FindSymbolReferences(root, symbol, semanticModel);

                if (hasReferences) continue;
                FileLinePositionSpan location = member.GetLocation().GetLineSpan();
                string memberName = symbol.Name;
                string kind = symbol.Kind == SymbolKind.Method ? "UnusedMethod" :
                    symbol.Kind == SymbolKind.Field ? "UnusedField" :
                    "UnusedProperty";

                response.DeadCode.Add(new DeadCodeInfo
                {
                    Kind = kind,
                    Name = memberName,
                    Line = location.StartLinePosition.Line + 1,
                    Column = location.StartLinePosition.Character + 1,
                    Message = $"Private {symbol.Kind.ToString().ToLower()} '{memberName}' is never used",
                    Suggestion = $"Consider removing this unused {symbol.Kind.ToString().ToLower()}"
                });
            }


            response.TotalCount = response.DeadCode.Count;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = $"Dead code analysis failed: {ex.Message}";
        }

        return response;
    }

    public static async Task<GetCodeFixesResponse> GetCodeFixesAsync(string code, string? filePath = null)
    {
        var response = new GetCodeFixesResponse { Success = true };

        try
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "temp.cs");
            SyntaxNode root = await syntaxTree.GetRootAsync();

            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                [syntaxTree],
                GetBasicReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Get diagnostics that have potential fixes
            List<Diagnostic> diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning)
                .ToList();

            foreach (Diagnostic diagnostic in diagnostics)
            {
                FileLinePositionSpan lineSpan = diagnostic.Location.GetLineSpan();
                string fixDescription = GetFixDescription(diagnostic.Id);
                string? fixedCode = TryApplySimpleFix(diagnostic, root, semanticModel);

                response.CodeFixes.Add(new CodeFixInfo
                {
                    DiagnosticId = diagnostic.Id,
                    DiagnosticMessage = diagnostic.GetMessage(),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    FixDescription = fixDescription,
                    FixedCode = fixedCode
                });
            }

            response.TotalCount = response.CodeFixes.Count;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = $"Code fixes analysis failed: {ex.Message}";
        }

        return response;
    }

    private static string GetFixDescription(string diagnosticId)
    {
        return diagnosticId switch
        {
            "CS0103" => "Add missing using directive or assembly reference",
            "CS0246" => "Add missing using directive or fix type name",
            "CS1002" => "Add missing semicolon",
            "CS1513" => "Add missing closing brace",
            "CS0029" => "Cast or convert the type to match",
            "CS0161" => "Add return statement",
            "CS0101" => "Rename to avoid name conflict",
            "CS0219" => "Remove unused variable or use it",
            "CS0168" => "Remove unused variable or use it",
            _ => "Refer to documentation for fix suggestions"
        };
    }

    private static string? TryApplySimpleFix(Diagnostic diagnostic, SyntaxNode root, SemanticModel semanticModel)
    {
        try
        {
            // For now, we'll return null - full code fixing requires the CodeFixProvider infrastructure
            // which is more complex. This is a placeholder for future enhancement.
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool FindSymbolReferences(SyntaxNode root, ISymbol targetSymbol, SemanticModel semanticModel)
    {
        // Search for any references to the target symbol in the syntax tree
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            // Skip the declaration itself
            ISymbol? declaredSymbol = semanticModel.GetDeclaredSymbol(node);
            if (SymbolEqualityComparer.Default.Equals(declaredSymbol, targetSymbol))
                continue;

            switch (node)
            {
                // Check identifiers
                case IdentifierNameSyntax identifier:
                {
                    RoslynSymbolInfo symbolInfo = ModelExtensions.GetSymbolInfo(semanticModel, identifier);
                    if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, targetSymbol))
                    {
                        return true;
                    }

                    break;
                }
                // Check member access (e.g., this.MyMethod)
                case MemberAccessExpressionSyntax memberAccess:
                {
                    RoslynSymbolInfo symbolInfo = ModelExtensions.GetSymbolInfo(semanticModel, memberAccess);
                    if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, targetSymbol))
                    {
                        return true;
                    }

                    break;
                }
            }
        }

        return false;
    }
}