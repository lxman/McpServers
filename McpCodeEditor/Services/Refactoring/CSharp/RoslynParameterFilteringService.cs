using McpCodeEditor.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.Refactoring.CSharp;

/// <summary>
/// Roslyn-based semantic parameter filtering service that replaces heuristic-based filtering
/// Uses actual semantic analysis to determine variable scope and declaration context
/// ROSLYN UPGRADE: Replaces regex/heuristic-based ParameterFilteringService
/// </summary>
public class RoslynParameterFilteringService : IParameterFilteringService
{
    private readonly ILogger<RoslynParameterFilteringService>? _logger;

    public RoslynParameterFilteringService(ILogger<RoslynParameterFilteringService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Filters variables using Roslyn semantic analysis to determine which should be passed as parameters
    /// </summary>
    public List<VariableInfo> FilterParametersToPass(
        List<VariableInfo> externalVariables,
        string[] extractedLines,
        string[]? fullFileLines = null)
    {
        _logger?.LogDebug("Filtering {Count} external variables using Roslyn semantic analysis", externalVariables.Count);

        if (fullFileLines == null || fullFileLines.Length == 0)
        {
            _logger?.LogWarning("No full file context provided - falling back to simple filtering");
            return externalVariables.Where(v => ShouldPassAsParameterFallback(v)).ToList();
        }

        try
        {
            return FilterUsingSemanticAnalysis(externalVariables, extractedLines, fullFileLines);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Roslyn semantic analysis failed - falling back to simple filtering");
            return externalVariables.Where(v => ShouldPassAsParameterFallback(v)).ToList();
        }
    }

    /// <summary>
    /// Core method using Roslyn semantic analysis to filter parameters
    /// </summary>
    private List<VariableInfo> FilterUsingSemanticAnalysis(
        List<VariableInfo> externalVariables, 
        string[] extractedLines, 
        string[] fullFileLines)
    {
        var parametersToPass = new List<VariableInfo>();

        // Parse the full file to get semantic context
        string fullFileContent = string.Join(Environment.NewLine, fullFileLines);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(fullFileContent);
        
        // Create a basic compilation to get semantic model
        CSharpCompilation compilation = CSharpCompilation.Create("AnalysisAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
        CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

        // Find the method containing the extraction
        MethodDeclarationSyntax? containingMethod = FindContainingMethod(root, extractedLines);
        if (containingMethod == null)
        {
            _logger?.LogWarning("Could not find containing method - using fallback filtering");
            return externalVariables.Where(v => ShouldPassAsParameterFallback(v)).ToList();
        }

        // Analyze each variable using semantic information
        foreach (VariableInfo variable in externalVariables)
        {
            VariableDeclarationContext context = AnalyzeVariableDeclarationContext(
                variable.Name, containingMethod, root, semanticModel);

            if (ShouldPassAsParameterSemantic(variable, context))
            {
                parametersToPass.Add(variable);
                _logger?.LogDebug("Variable {Name} will be passed as parameter - {Context}", 
                    variable.Name, context.ToString());
            }
            else
            {
                _logger?.LogDebug("Variable {Name} will NOT be passed as parameter - {Context}", 
                    variable.Name, context.ToString());
            }
        }

        return parametersToPass;
    }

    /// <summary>
    /// Finds the method declaration that contains the extracted lines
    /// </summary>
    private MethodDeclarationSyntax? FindContainingMethod(CompilationUnitSyntax root, string[] extractedLines)
    {
        if (extractedLines.Length == 0) return null;

        // Look for a method that contains similar content to the extracted lines
        string firstExtractedLine = extractedLines[0].Trim();
        string lastExtractedLine = extractedLines[^1].Trim();

        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            string methodText = method.Body?.ToString() ?? "";
            
            // Check if the method contains patterns from our extracted lines
            if (extractedLines.Any(line => 
                !string.IsNullOrWhiteSpace(line.Trim()) && 
                methodText.Contains(line.Trim().Split()[0]))) // Match first word
            {
                return method;
            }
        }

        // Fallback: return the first method found
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
    }

    /// <summary>
    /// Analyzes the declaration context of a variable using Roslyn semantic analysis
    /// </summary>
    private VariableDeclarationContext AnalyzeVariableDeclarationContext(
        string variableName, 
        MethodDeclarationSyntax containingMethod, 
        CompilationUnitSyntax root,
        SemanticModel semanticModel)
    {
        // 1. Check if it's a method parameter
        if (IsMethodParameter(variableName, containingMethod))
        {
            return new VariableDeclarationContext
            {
                VariableName = variableName,
                DeclarationType = VariableDeclarationType.MethodParameter,
                Scope = VariableScope.Parameter,
                ShouldPassAsParameter = false, // Already a parameter
                Reason = "Already a method parameter"
            };
        }

        // 2. Check if it's a local variable in the method
        VariableDeclarationContext? localVarContext = FindLocalVariableDeclaration(variableName, containingMethod, semanticModel);
        if (localVarContext != null)
        {
            return localVarContext;
        }

        // 3. Check if it's a class field or property
        VariableDeclarationContext? fieldContext = FindFieldOrPropertyDeclaration(variableName, root, semanticModel);
        if (fieldContext != null)
        {
            return fieldContext;
        }

        // 4. Check if it's a static member access
        if (IsStaticMemberAccess(variableName))
        {
            return new VariableDeclarationContext
            {
                VariableName = variableName,
                DeclarationType = VariableDeclarationType.StaticMember,
                Scope = VariableScope.Static,
                ShouldPassAsParameter = false,
                Reason = "Static member or type"
            };
        }

        // 5. Unknown - default to external local variable
        return new VariableDeclarationContext
        {
            VariableName = variableName,
            DeclarationType = VariableDeclarationType.Unknown,
            Scope = VariableScope.External,
            ShouldPassAsParameter = true,
            Reason = "Unknown variable - assuming external local variable"
        };
    }

    /// <summary>
    /// Checks if a variable is a method parameter
    /// </summary>
    private static bool IsMethodParameter(string variableName, MethodDeclarationSyntax method)
    {
        return method.ParameterList.Parameters
            .Any(p => p.Identifier.ValueText == variableName);
    }

    /// <summary>
    /// Finds local variable declarations within the method
    /// </summary>
    private VariableDeclarationContext? FindLocalVariableDeclaration(
        string variableName, 
        MethodDeclarationSyntax method,
        SemanticModel semanticModel)
    {
        // Look for variable declarations in the method body
        if (method.Body == null) return null;

        foreach (VariableDeclarationSyntax varDecl in method.Body.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            foreach (VariableDeclaratorSyntax variable in varDecl.Variables)
            {
                if (variable.Identifier.ValueText == variableName)
                {
                    // Found the declaration - get type information
                    SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(varDecl.Type);
                    string variableType = symbolInfo.Symbol?.ToDisplayString() ?? varDecl.Type.ToString();

                    return new VariableDeclarationContext
                    {
                        VariableName = variableName,
                        DeclarationType = VariableDeclarationType.LocalVariable,
                        Scope = VariableScope.Local,
                        ShouldPassAsParameter = true, // Local variables need to be passed
                        VariableType = variableType,
                        Reason = "Local variable declared in method"
                    };
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds field or property declarations at class level
    /// </summary>
    private VariableDeclarationContext? FindFieldOrPropertyDeclaration(
        string variableName, 
        CompilationUnitSyntax root,
        SemanticModel semanticModel)
    {
        // Look for field declarations
        foreach (FieldDeclarationSyntax field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                if (variable.Identifier.ValueText == variableName)
                {
                    bool isStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                    var variableType = field.Declaration.Type.ToString();

                    return new VariableDeclarationContext
                    {
                        VariableName = variableName,
                        DeclarationType = isStatic ? VariableDeclarationType.StaticField : VariableDeclarationType.InstanceField,
                        Scope = isStatic ? VariableScope.Static : VariableScope.Instance,
                        ShouldPassAsParameter = false, // Fields are accessible directly
                        VariableType = variableType,
                        Reason = isStatic ? "Static field" : "Instance field"
                    };
                }
            }
        }

        // Look for property declarations
        foreach (PropertyDeclarationSyntax property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (property.Identifier.ValueText == variableName)
            {
                bool isStatic = property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                var variableType = property.Type.ToString();

                return new VariableDeclarationContext
                {
                    VariableName = variableName,
                    DeclarationType = isStatic ? VariableDeclarationType.StaticProperty : VariableDeclarationType.InstanceProperty,
                    Scope = isStatic ? VariableScope.Static : VariableScope.Instance,
                    ShouldPassAsParameter = false, // Properties are accessible directly
                    VariableType = variableType,
                    Reason = isStatic ? "Static property" : "Instance property"
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a variable represents static member access (e.g., Console.WriteLine, Math.Max)
    /// </summary>
    private static bool IsStaticMemberAccess(string variableName)
    {
        // Common static types
        var commonStaticTypes = new HashSet<string>
        {
            "Console", "Math", "DateTime", "String", "Convert", "Environment",
            "File", "Directory", "Path", "Debug", "Trace", "Encoding",
            "Regex", "Random", "Guid", "TimeSpan", "Thread", "Task"
        };

        if (commonStaticTypes.Contains(variableName))
            return true;

        // Check for Type.Member pattern
        if (variableName.Contains('.'))
        {
            string typeName = variableName.Split('.')[0];
            return commonStaticTypes.Contains(typeName) || char.IsUpper(typeName[0]);
        }

        return false;
    }

    /// <summary>
    /// Determines if a specific variable should be passed as a parameter (interface implementation)
    /// </summary>
    public bool ShouldPassAsParameter(VariableInfo variable, string[] extractedLines, string[]? fullFileLines)
    {
        if (fullFileLines == null || fullFileLines.Length == 0)
        {
            return ShouldPassAsParameterFallback(variable);
        }

        try
        {
            // Use the same semantic analysis as FilterParametersToPass
            string fullFileContent = string.Join(Environment.NewLine, fullFileLines);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(fullFileContent);
            
            CSharpCompilation compilation = CSharpCompilation.Create("AnalysisAssembly")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);
            
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

            MethodDeclarationSyntax? containingMethod = FindContainingMethod(root, extractedLines);
            if (containingMethod == null)
            {
                return ShouldPassAsParameterFallback(variable);
            }

            VariableDeclarationContext context = AnalyzeVariableDeclarationContext(
                variable.Name, containingMethod, root, semanticModel);

            return ShouldPassAsParameterSemantic(variable, context);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Semantic analysis failed for variable {Name} - using fallback", variable.Name);
            return ShouldPassAsParameterFallback(variable);
        }
    }

    /// <summary>
    /// Makes the filtering decision based on semantic analysis
    /// </summary>
    private bool ShouldPassAsParameterSemantic(VariableInfo variable, VariableDeclarationContext context)
    {
        return context.ShouldPassAsParameter;
    }

    /// <summary>
    /// Fallback method when Roslyn analysis fails
    /// </summary>
    private bool ShouldPassAsParameterFallback(VariableInfo variable)
    {
        string varName = variable.Name;

        // Simple fallback rules
        if (varName.StartsWith("_")) return false; // Underscore fields
        if (IsStaticMemberAccess(varName)) return false; // Static members
        if (varName.Contains(".") && char.IsUpper(varName[0])) return false; // Type.Member

        return true; // Default: pass as parameter
    }
    
    #region Type Inference (keeping existing functionality)

    /// <summary>
    /// Gets suggested parameter types based on semantic analysis
    /// </summary>
    public Dictionary<string, string> SuggestParameterTypes(
        List<VariableInfo> parameters,
        string[] extractedLines)
    {
        var suggestedTypes = new Dictionary<string, string>();
        
        foreach (VariableInfo param in parameters)
        {
            // Use the type from VariableInfo if available, otherwise infer
            string suggestedType = !string.IsNullOrEmpty(param.Type) && param.Type != "object" 
                ? param.Type 
                : InferParameterType(param.Name, extractedLines);
                
            suggestedTypes[param.Name] = suggestedType;
            _logger?.LogDebug("Suggested type for parameter {Name}: {Type}", param.Name, suggestedType);
        }
        
        return suggestedTypes;
    }

    /// <summary>
    /// Infers parameter type based on usage patterns (enhanced version)
    /// </summary>
    private static string InferParameterType(string paramName, string[] extractedLines)
    {
        string extractedCode = string.Join(" ", extractedLines);
        
        // Collection patterns (more accurate)
        if (extractedCode.Contains($"foreach") && extractedCode.Contains($"in {paramName}"))
        {
            // Try to determine collection type from context
            if (paramName.ToLower().Contains("order")) return "List<Order>";
            if (paramName.ToLower().Contains("customer")) return "List<Customer>";
            if (paramName.ToLower().Contains("item")) return "List<Item>";
            return "IEnumerable<object>";
        }

        // Numeric operations
        if (extractedCode.Contains($"{paramName}++") || 
            extractedCode.Contains($"++{paramName}") ||
            extractedCode.Contains($"{paramName} + ") ||
            extractedCode.Contains($"{paramName} - "))
        {
            // Check for decimal operations
            if (extractedCode.Contains($"{paramName} += ") && paramName.ToLower().Contains("total"))
                return "decimal";
            return "int";
        }

        // Property access patterns
        if (extractedCode.Contains($"{paramName}.Count") || extractedCode.Contains($"{paramName}.Length"))
            return extractedCode.Contains($"{paramName}.Length") ? "string" : "ICollection";

        // Naming convention fallbacks
        string lowerName = paramName.ToLower();
        if (lowerName.Contains("count") || lowerName.Contains("index") || lowerName.Contains("id")) return "int";
        if (lowerName.Contains("total") || lowerName.Contains("amount") || lowerName.Contains("price")) return "decimal";
        if (lowerName.Contains("name") || lowerName.Contains("text") || lowerName.Contains("message")) return "string";
        if (lowerName.StartsWith("is") || lowerName.StartsWith("has") || lowerName.StartsWith("can")) return "bool";
        if (lowerName.EndsWith("s") && lowerName.Length > 2) return "IEnumerable";
        
        return "object";
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Represents the declaration context of a variable as determined by Roslyn semantic analysis
/// </summary>
public class VariableDeclarationContext
{
    public string VariableName { get; set; } = string.Empty;
    public VariableDeclarationType DeclarationType { get; set; }
    public VariableScope Scope { get; set; }
    public bool ShouldPassAsParameter { get; set; }
    public string VariableType { get; set; } = "object";
    public string Reason { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{DeclarationType} ({Scope}) - {Reason}";
    }
}

/// <summary>
/// Types of variable declarations that can be detected by semantic analysis
/// </summary>
public enum VariableDeclarationType
{
    LocalVariable,
    MethodParameter,
    InstanceField,
    StaticField,
    InstanceProperty,
    StaticProperty,
    StaticMember,
    Unknown
}

#endregion