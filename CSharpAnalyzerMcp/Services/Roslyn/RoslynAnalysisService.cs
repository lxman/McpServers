using System.Collections.Immutable;
using System.Reflection;
using CSharpAnalyzerMcp.Models.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SymbolInfo = CSharpAnalyzerMcp.Models.Roslyn.SymbolInfo;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;
using RoslynSymbolInfo = Microsoft.CodeAnalysis.SymbolInfo;

namespace CSharpAnalyzerMcp.Services.Roslyn;

public class RoslynAnalysisService
{
    public static async Task<AnalyzeCodeResponse> AnalyzeCodeAsync(string code, string? filePath = null)
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

    public async Task<GetSymbolsResponse> GetSymbolsAsync(string code, string? filePath = null, string? filter = null)
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
            // Return empty response with error logged
            Console.Error.WriteLine($"GetSymbols failed: {ex.Message}");
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
            SyntaxNode formattedRoot = Microsoft.CodeAnalysis.Formatting.Formatter.Format(
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
        int complexity = 1;

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
                binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression) ||
                binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression))
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
            typeof(System.Linq.Enumerable).Assembly,
            typeof(System.Collections.Generic.List<>).Assembly
        ];

        return assemblies
            .Where(a => !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
    }
}