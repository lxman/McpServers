using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpCodeEditor.Services.Validation;

/// <summary>
/// Enhanced semantic analyzer for method extraction return value detection using Roslyn DataFlowAnalysis
/// </summary>
public class SemanticReturnAnalyzer
{
    /// <summary>
    /// Result of return value analysis
    /// </summary>
    public class ReturnAnalysisResult
    {
        public string SuggestedReturnType { get; set; } = "void";
        public bool RequiresReturnValue { get; set; }
        public List<string> ParametersNeeded { get; set; } = [];
        public List<string> VariablesFlowingOut { get; set; } = [];
        public List<string> VariablesFlowingIn { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
        public string ReturnTypeReason { get; set; } = "";
    }

    /// <summary>
    /// Analyzes return value requirements using Roslyn's DataFlowAnalysis
    /// </summary>
    public static async Task<ReturnAnalysisResult> AnalyzeReturnRequirementsAsync(
        string sourceCode,
        int startLine,
        int endLine,
        CancellationToken cancellationToken = default)
    {
        var result = new ReturnAnalysisResult();

        try
        {
            // Create semantic model with metadata references
            CSharpCompilation compilation = await CreateSemanticCompilationAsync(sourceCode, cancellationToken);
            SyntaxTree syntaxTree = compilation.SyntaxTrees.First();
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
            SyntaxNode root = await syntaxTree.GetRootAsync(cancellationToken);

            // Find the containing method and the selection region
            MethodDeclarationSyntax? containingMethod = FindContainingMethod(root, startLine);
            if (containingMethod == null)
            {
                result.Warnings.Add("Could not find containing method for semantic analysis");
                return result;
            }

            // Get the statements in the selection region
            List<StatementSyntax> selectedStatements = GetSelectedStatements(containingMethod, startLine, endLine);
            if (selectedStatements.Count == 0)
            {
                result.Warnings.Add("No statements found in selected region");
                return result;
            }

            // Create a region for data flow analysis
            StatementSyntax firstStatement = selectedStatements.First();
            StatementSyntax lastStatement = selectedStatements.Last();

            // Perform Roslyn data flow analysis
            DataFlowAnalysis? dataFlowAnalysis = semanticModel.AnalyzeDataFlow(firstStatement, lastStatement);

            if (dataFlowAnalysis.Succeeded)
            {
                // Analyze variables flowing into the region (parameters needed)
                foreach (ISymbol symbol in dataFlowAnalysis.DataFlowsIn)
                {
                    ITypeSymbol? symbolType = GetSymbolType(symbol);
                    if (symbolType != null)
                    {
                        if (symbol is ILocalSymbol)
                        {
                            string typeName = symbolType.ToDisplayString();
                            var parameterSpec = $"{typeName} {symbol.Name}";
                            result.ParametersNeeded.Add(parameterSpec);
                            result.VariablesFlowingIn.Add(symbol.Name);
                        }
                        else if (symbol is IParameterSymbol)
                        {
                            string typeName = symbolType.ToDisplayString();
                            var parameterSpec = $"{typeName} {symbol.Name}";
                            result.ParametersNeeded.Add(parameterSpec);
                            result.VariablesFlowingIn.Add(symbol.Name);
                        }
                    }
                }

                // Analyze variables flowing out of the region (return value needed)
                foreach (ISymbol symbol in dataFlowAnalysis.DataFlowsOut)
                {
                    if (symbol is ILocalSymbol localSymbol)
                    {
                        result.VariablesFlowingOut.Add(localSymbol.Name);
                    }
                }

                // Determine return type based on data flow analysis
                result = DetermineReturnTypeFromDataFlow(result, dataFlowAnalysis, selectedStatements, semanticModel);
            }
            else
            {
                result.Warnings.Add("Data flow analysis failed - falling back to basic analysis");
                // Fallback to basic analysis
                result = PerformBasicReturnAnalysis(selectedStatements, semanticModel, result);
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Semantic analysis failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Creates a semantic compilation with proper metadata references
    /// </summary>
    private static async Task<CSharpCompilation> CreateSemanticCompilationAsync(
        string sourceCode, 
        CancellationToken cancellationToken)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

        // Add essential metadata references for semantic analysis
        PortableExecutableReference[] references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        ];

        var compilation = CSharpCompilation.Create(
            "TempAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    /// <summary>
    /// Finds the containing method for the selection
    /// </summary>
    private static MethodDeclarationSyntax? FindContainingMethod(SyntaxNode root, int startLine)
    {
        IEnumerable<MethodDeclarationSyntax> methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (MethodDeclarationSyntax method in methods)
        {
            FileLinePositionSpan span = method.GetLocation().GetLineSpan();
            int methodStartLine = span.StartLinePosition.Line + 1; // Convert to 1-based
            int methodEndLine = span.EndLinePosition.Line + 1;

            if (startLine >= methodStartLine && startLine <= methodEndLine)
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the statements within the selected line range
    /// </summary>
    private static List<StatementSyntax> GetSelectedStatements(
        MethodDeclarationSyntax method, 
        int startLine, 
        int endLine)
    {
        var selectedStatements = new List<StatementSyntax>();

        if (method.Body != null)
        {
            foreach (StatementSyntax statement in method.Body.Statements)
            {
                FileLinePositionSpan span = statement.GetLocation().GetLineSpan();
                int statementStartLine = span.StartLinePosition.Line + 1; // Convert to 1-based
                int statementEndLine = span.EndLinePosition.Line + 1; // Convert to 1-based
                
                // FIXED: Check if statement overlaps with the selection range
                // Include if:
                // - Statement starts within the range, OR
                // - Statement ends within the range, OR  
                // - Statement spans the entire range
                bool startsInRange = statementStartLine >= startLine && statementStartLine <= endLine;
                bool endsInRange = statementEndLine >= startLine && statementEndLine <= endLine;
                bool spansRange = statementStartLine <= startLine && statementEndLine >= endLine;
                
                if (startsInRange || endsInRange || spansRange)
                {
                    selectedStatements.Add(statement);
                }
            }
        }

        return selectedStatements;
    }

    /// <summary>
    /// Determines return type based on Roslyn data flow analysis
    /// </summary>
    private static ReturnAnalysisResult DetermineReturnTypeFromDataFlow(
        ReturnAnalysisResult result,
        DataFlowAnalysis dataFlowAnalysis,
        List<StatementSyntax> selectedStatements,
        SemanticModel semanticModel)
    {
        // Check if there are explicit return statements
        bool hasReturnStatements = selectedStatements
            .SelectMany(s => s.DescendantNodes())
            .OfType<ReturnStatementSyntax>()
            .Any();

        if (hasReturnStatements)
        {
            // If there are return statements, analyze their types
            List<string> returnTypes = AnalyzeReturnStatementTypes(selectedStatements, semanticModel);
            if (returnTypes.Count == 1)
            {
                result.SuggestedReturnType = returnTypes.First();
                result.RequiresReturnValue = true;
                result.ReturnTypeReason = "Code contains return statements";
            }
            else if (returnTypes.Count > 1)
            {
                result.SuggestedReturnType = "object"; // Common base type
                result.RequiresReturnValue = true;
                result.ReturnTypeReason = "Code contains return statements with multiple types";
            }
            return result;
        }

        // If no return statements, check data flow out
        if (dataFlowAnalysis.DataFlowsOut.Length == 1)
        {
            // Single variable flows out - perfect for return value
            ISymbol symbol = dataFlowAnalysis.DataFlowsOut[0];
            ITypeSymbol? symbolType = GetSymbolType(symbol);
            if (symbolType != null)
            {
                result.SuggestedReturnType = symbolType.ToDisplayString();
                result.RequiresReturnValue = true;
                result.ReturnTypeReason = $"Variable '{symbol.Name}' is modified and used after the extracted code";
            }
        }
        else if (dataFlowAnalysis.DataFlowsOut.Length > 1)
        {
            // Multiple variables flow out - suggest tuple or keep void with ref parameters
            List<string> types = dataFlowAnalysis.DataFlowsOut
                .Select(GetSymbolType)
                .Where(t => t != null)
                .Select(t => t!.ToDisplayString())
                .ToList();

            if (types.Count <= 3) // Reasonable tuple size
            {
                result.SuggestedReturnType = $"({string.Join(", ", types)})";
                result.RequiresReturnValue = true;
                result.ReturnTypeReason = $"Multiple variables ({string.Join(", ", dataFlowAnalysis.DataFlowsOut.Select(s => s.Name))}) are modified";
            }
            else
            {
                result.SuggestedReturnType = "void";
                result.RequiresReturnValue = false;
                result.ReturnTypeReason = "Too many variables modified - use ref parameters instead";
                result.Warnings.Add($"Code modifies {types.Count} variables. Consider using ref parameters or smaller extraction.");
            }
        }
        else
        {
            // No data flows out - void is appropriate
            result.SuggestedReturnType = "void";
            result.RequiresReturnValue = false;
            result.ReturnTypeReason = "No variables are modified or flow out of the selected code";
        }

        return result;
    }

    /// <summary>
    /// Gets the type from an ISymbol, handling different symbol types
    /// </summary>
    private static ITypeSymbol? GetSymbolType(ISymbol symbol)
    {
        return symbol switch
        {
            ILocalSymbol localSymbol => localSymbol.Type,
            IParameterSymbol paramSymbol => paramSymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            IPropertySymbol propertySymbol => propertySymbol.Type,
            _ => null
        };
    }

    /// <summary>
    /// Analyzes return statement types in the selected code
    /// </summary>
    private static List<string> AnalyzeReturnStatementTypes(
        List<StatementSyntax> selectedStatements,
        SemanticModel semanticModel)
    {
        var returnTypes = new HashSet<string>();

        IEnumerable<ReturnStatementSyntax> returnStatements = selectedStatements
            .SelectMany(s => s.DescendantNodes())
            .OfType<ReturnStatementSyntax>();

        foreach (ReturnStatementSyntax returnStatement in returnStatements)
        {
            if (returnStatement.Expression != null)
            {
                TypeInfo typeInfo = semanticModel.GetTypeInfo(returnStatement.Expression);
                if (typeInfo.Type != null)
                {
                    returnTypes.Add(typeInfo.Type.ToDisplayString());
                }
            }
            else
            {
                returnTypes.Add("void");
            }
        }

        return returnTypes.ToList();
    }

    /// <summary>
    /// Fallback basic analysis when data flow analysis fails
    /// </summary>
    private static ReturnAnalysisResult PerformBasicReturnAnalysis(
        List<StatementSyntax> selectedStatements,
        SemanticModel semanticModel,
        ReturnAnalysisResult result)
    {
        // Check for return statements
        bool hasReturnStatements = selectedStatements
            .SelectMany(s => s.DescendantNodes())
            .OfType<ReturnStatementSyntax>()
            .Any();

        if (hasReturnStatements)
        {
            result.SuggestedReturnType = "object"; // Safe default
            result.RequiresReturnValue = true;
            result.ReturnTypeReason = "Code contains return statements (basic analysis)";
        }
        else
        {
            result.SuggestedReturnType = "void";
            result.RequiresReturnValue = false;
            result.ReturnTypeReason = "No return statements detected (basic analysis)";
        }

        return result;
    }
}
