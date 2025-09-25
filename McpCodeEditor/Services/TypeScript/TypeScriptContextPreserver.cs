using Microsoft.Extensions.Logging;
using System.Text;
using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Services.TypeScript;

/// <summary>
/// Service that preserves context and scope during TypeScript method extraction
/// Ensures that 'this' references, class members, and variable scope are maintained
/// </summary>
public class TypeScriptContextPreserver
{
    private readonly ILogger<TypeScriptContextPreserver> _logger;
    private readonly TypeScriptAstParserService _astParser;
    
    public TypeScriptContextPreserver(
        ILogger<TypeScriptContextPreserver> logger,
        TypeScriptAstParserService astParser)
    {
        _logger = logger;
        _astParser = astParser;
    }

    /// <summary>
    /// Analyze and preserve context for method extraction
    /// </summary>
    public async Task<ContextPreservationResult> PreserveContextAsync(
        string sourceCode,
        int startLine,
        int endLine,
        string fileName = "temp.ts")
    {
        try
        {
            _logger.LogDebug("Preserving context for lines {Start}-{End}", startLine, endLine);
            
            // Get full AST and scope analysis
            var ast = await _astParser.ParseAsync(sourceCode, fileName);
            var scope = await _astParser.AnalyzeScopeAsync(sourceCode, startLine, endLine, fileName);
            var methodInfo = await _astParser.ExtractMethodInfoAsync(sourceCode, startLine, endLine, fileName);
            
            var result = new ContextPreservationResult
            {
                Success = true,
                ScopeAnalysis = scope,
                MethodInfo = methodInfo
            };
            
            // Analyze this references
            if (methodInfo?.ThisReferences?.Count > 0)
            {
                result.RequiresThisContext = true;
                result.ThisReferences = methodInfo.ThisReferences;
                
                // Determine if we need to pass 'this' or bind context
                if (scope.ThisContext is "class" or "method")
                {
                    result.ThisContextType = scope.ThisContext;
                    result.ParentClassName = scope.ParentClass?.Name;
                    
                    // Check which class members are accessed
                    foreach (var thisRef in methodInfo.ThisReferences)
                    {
                        var member = scope.ParentClass?.Members?.FirstOrDefault(m => m.Name == thisRef.Property);
                        if (member != null)
                        {
                            result.AccessedClassMembers.Add(new AccessedMember
                            {
                                Name = member.Name,
                                Kind = member.Kind,
                                IsPrivate = member.IsPrivate,
                                IsProtected = member.IsProtected,
                                IsStatic = member.IsStatic
                            });
                        }
                    }
                }
            }
            
            // Analyze closure variables
            if (scope.ClosureVariables?.Count > 0)
            {
                result.RequiresClosureVariables = true;
                result.ClosureVariables = scope.ClosureVariables;
                
                // Determine which closure variables need to be parameters
                var modifiedVars = methodInfo?.ModifiedVariables ?? [];
                foreach (var closureVar in scope.ClosureVariables)
                {
                    result.RequiredParameters.Add(new RequiredParameter
                    {
                        Name = closureVar,
                        Type = "any", // TODO: Infer type from usage
                        IsModified = modifiedVars.Contains(closureVar),
                        Source = "closure"
                    });
                }
            }
            
            // Analyze local variables that might need to be parameters
            if (methodInfo?.UsedVariables?.Count > 0)
            {
                var localVarNames = scope.LocalVariables.Select(v => v.Name).ToHashSet();
                var paramNames = methodInfo.Parameters.Select(p => p.Name).ToHashSet();
                
                foreach (var usedVar in methodInfo.UsedVariables)
                {
                    // If variable is used but not defined locally or as parameter
                    if (!localVarNames.Contains(usedVar) && 
                        !paramNames.Contains(usedVar) && 
                        !result.RequiredParameters.Any(p => p.Name == usedVar))
                    {
                        // Check if it's a global or imported symbol
                        if (!IsGlobalSymbol(usedVar) && !IsImportedSymbol(usedVar, scope.Imports))
                        {
                            result.RequiredParameters.Add(new RequiredParameter
                            {
                                Name = usedVar,
                                Type = "any",
                                IsModified = methodInfo.ModifiedVariables?.Contains(usedVar) ?? false,
                                Source = "external"
                            });
                        }
                    }
                }
            }
            
            // Generate preservation strategies
            result.PreservationStrategies = GeneratePreservationStrategies(result);
            
            // Generate extraction guidance
            result.ExtractionGuidance = GenerateExtractionGuidance(result);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preserve context");
            return new ContextPreservationResult
            {
                Success = false,
                ErrorMessage = $"Context preservation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate code that preserves context for the extracted method
    /// </summary>
    public static string GenerateContextPreservingCall(
        ContextPreservationResult context,
        string methodName,
        bool isAsync = false)
    {
        var callBuilder = new StringBuilder();
        
        // Handle this context
        if (context is { RequiresThisContext: true, ThisContextType: "method" })
        {
            // For methods, we need to call with 'this'
            callBuilder.Append("this.");
        }
        
        // Add await if needed
        if (isAsync || context.MethodInfo?.HasAwait == true)
        {
            callBuilder.Insert(0, "await ");
        }
        
        // Add method name
        callBuilder.Append(methodName);
        callBuilder.Append("(");
        
        // Add parameters
        var parameters = new List<string>();
        
        // Add required parameters from context
        foreach (var param in context.RequiredParameters)
        {
            parameters.Add(param.Name);
        }
        
        // If we need to pass 'this' context explicitly (for arrow functions, etc.)
        if (context.RequiresThisContext && context.PreservationStrategies.Contains("PASS_THIS_AS_PARAMETER"))
        {
            parameters.Insert(0, "this");
        }
        
        callBuilder.Append(string.Join(", ", parameters));
        callBuilder.Append(")");
        
        // Handle return values
        if (context.MethodInfo?.HasReturnStatement == true)
        {
            callBuilder.Insert(0, "return ");
        }
        else if (context.RequiredParameters.Any(p => p.IsModified))
        {
            // If variables are modified, we might need to capture the return
            var modifiedParams = context.RequiredParameters.Where(p => p.IsModified).ToList();
            if (modifiedParams.Count == 1)
            {
                callBuilder.Insert(0, $"{modifiedParams[0].Name} = ");
            }
            else if (modifiedParams.Count > 1)
            {
                var destructuring = string.Join(", ", modifiedParams.Select(p => p.Name));
                callBuilder.Insert(0, $"{{ {destructuring} }} = ");
            }
        }
        
        callBuilder.Append(";");
        
        return callBuilder.ToString();
    }

    /// <summary>
    /// Generate the extracted method with proper context preservation
    /// </summary>
    public static string GenerateContextPreservingMethod(
        ContextPreservationResult context,
        string methodName,
        string extractedCode,
        TypeScriptFunctionType functionType,
        bool isAsync = false)
    {
        var methodBuilder = new StringBuilder();
        
        // Add JSDoc comment
        methodBuilder.AppendLine("/**");
        methodBuilder.AppendLine($" * Extracted method: {methodName}");
        
        // Document parameters
        foreach (var param in context.RequiredParameters)
        {
            methodBuilder.AppendLine($" * @param {param.Name} {param.Type} - {param.Source} variable");
        }
        
        // Document this context if needed
        if (context.RequiresThisContext && context.PreservationStrategies.Contains("PASS_THIS_AS_PARAMETER"))
        {
            methodBuilder.AppendLine($" * @param context {context.ParentClassName} - Class context for 'this' references");
        }
        
        // Document return type
        if (context.MethodInfo?.ReturnType != null && context.MethodInfo.ReturnType != "void")
        {
            methodBuilder.AppendLine($" * @returns {context.MethodInfo.ReturnType}");
        }
        
        methodBuilder.AppendLine(" */");
        
        // Generate method signature
        var signature = GenerateMethodSignature(context, methodName, functionType, isAsync);
        methodBuilder.Append(signature);
        methodBuilder.AppendLine(" {");
        
        // If we need to handle 'this' context for arrow functions
        if (context.RequiresThisContext && functionType == TypeScriptFunctionType.ArrowFunction)
        {
            // Arrow functions will preserve 'this' from lexical scope
            // No special handling needed in the method body
        }
        
        // Add the extracted code with proper indentation
        var codeLines = extractedCode.Split('\n');
        foreach (var line in codeLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                methodBuilder.AppendLine($"    {line}");
            }
            else
            {
                methodBuilder.AppendLine();
            }
        }
        
        // Add return statement if needed
        if (context.RequiredParameters.Any(p => p.IsModified))
        {
            var modifiedParams = context.RequiredParameters.Where(p => p.IsModified).ToList();
            if (modifiedParams.Count == 1)
            {
                methodBuilder.AppendLine($"    return {modifiedParams[0].Name};");
            }
            else if (modifiedParams.Count > 1)
            {
                var returnObj = string.Join(", ", modifiedParams.Select(p => p.Name));
                methodBuilder.AppendLine($"    return {{ {returnObj} }};");
            }
        }
        
        methodBuilder.AppendLine("}");
        
        return methodBuilder.ToString();
    }

    private static string GenerateMethodSignature(
        ContextPreservationResult context,
        string methodName,
        TypeScriptFunctionType functionType,
        bool isAsync)
    {
        var signatureBuilder = new StringBuilder();
        
        // Handle async
        var needsAsync = isAsync || context.MethodInfo?.HasAwait == true;
        
        switch (functionType)
        {
            case TypeScriptFunctionType.Function:
                if (needsAsync) signatureBuilder.Append("async ");
                signatureBuilder.Append($"function {methodName}");
                break;
                
            case TypeScriptFunctionType.ArrowFunction:
                signatureBuilder.Append($"const {methodName} = ");
                if (needsAsync) signatureBuilder.Append("async ");
                break;
                
            case TypeScriptFunctionType.Method:
                if (needsAsync) signatureBuilder.Append("async ");
                signatureBuilder.Append(methodName);
                break;
                
            case TypeScriptFunctionType.AsyncFunction:
                signatureBuilder.Append($"async function {methodName}");
                break;
                
            case TypeScriptFunctionType.AsyncArrowFunction:
                signatureBuilder.Append($"const {methodName} = async ");
                break;
                
            default:
                signatureBuilder.Append($"function {methodName}");
                break;
        }
        
        // Add parameters
        signatureBuilder.Append("(");
        
        var parameters = new List<string>();
        
        // Add 'this' context parameter if needed
        if (context.RequiresThisContext && context.PreservationStrategies.Contains("PASS_THIS_AS_PARAMETER"))
        {
            parameters.Add($"context: {context.ParentClassName ?? "any"}");
        }
        
        // Add required parameters
        foreach (var param in context.RequiredParameters)
        {
            parameters.Add($"{param.Name}: {param.Type}");
        }
        
        signatureBuilder.Append(string.Join(", ", parameters));
        signatureBuilder.Append(")");
        
        // Add return type
        if (context.MethodInfo?.ReturnType != null)
        {
            var returnType = context.MethodInfo.ReturnType;
            
            // Adjust for async functions
            if (needsAsync && !returnType.StartsWith("Promise"))
            {
                returnType = returnType == "void" ? "Promise<void>" : $"Promise<{returnType}>";
            }
            
            signatureBuilder.Append($": {returnType}");
        }
        
        // Add arrow for arrow functions
        if (functionType is TypeScriptFunctionType.ArrowFunction or TypeScriptFunctionType.AsyncArrowFunction)
        {
            signatureBuilder.Append(" =>");
        }
        
        return signatureBuilder.ToString();
    }

    private static List<string> GeneratePreservationStrategies(ContextPreservationResult context)
    {
        var strategies = new List<string>();
        
        if (context.RequiresThisContext)
        {
            if (context.ThisContextType is "class" or "method")
            {
                // For class methods, 'this' is available
                strategies.Add("USE_CLASS_THIS");
            }
            else
            {
                // For standalone functions, we need to pass context
                strategies.Add("PASS_THIS_AS_PARAMETER");
            }
        }
        
        if (context.RequiresClosureVariables)
        {
            strategies.Add("PASS_CLOSURE_AS_PARAMETERS");
        }
        
        if (context.RequiredParameters.Any(p => p.IsModified))
        {
            strategies.Add("RETURN_MODIFIED_VALUES");
        }
        
        if (context.MethodInfo?.HasAwait == true)
        {
            strategies.Add("PRESERVE_ASYNC_CONTEXT");
        }
        
        return strategies;
    }

    private static ExtractionGuidance GenerateExtractionGuidance(ContextPreservationResult context)
    {
        var guidance = new ExtractionGuidance();
        
        // Determine recommended function type
        if (context.RequiresThisContext)
        {
            if (context.ThisContextType is "class" or "method")
            {
                guidance.RecommendedFunctionType = TypeScriptFunctionType.Method;
                guidance.Reason = "Code uses 'this' references within a class context";
            }
            else
            {
                guidance.RecommendedFunctionType = TypeScriptFunctionType.ArrowFunction;
                guidance.Reason = "Arrow function will preserve 'this' from lexical scope";
            }
        }
        else
        {
            guidance.RecommendedFunctionType = TypeScriptFunctionType.Function;
            guidance.Reason = "Standard function is suitable for this extraction";
        }
        
        // Add warnings
        if (context.AccessedClassMembers.Any(m => m.IsPrivate))
        {
            guidance.Warnings.Add("Extracted method accesses private class members");
        }
        
        if (context.RequiresClosureVariables)
        {
            guidance.Warnings.Add($"Method requires {context.ClosureVariables.Count} closure variables as parameters");
        }
        
        if (context.RequiredParameters.Count(p => p.IsModified) > 3)
        {
            guidance.Warnings.Add("Multiple variables are modified - consider refactoring to reduce complexity");
        }
        
        // Add suggestions
        if (context.MethodInfo?.Complexity > 10)
        {
            guidance.Suggestions.Add("High complexity detected - consider breaking into smaller functions");
        }
        
        if (context.RequiredParameters.Count > 5)
        {
            guidance.Suggestions.Add("Many parameters required - consider passing an options object");
        }
        
        return guidance;
    }

    private static bool IsGlobalSymbol(string symbol)
    {
        // Check if symbol is a global TypeScript/JavaScript symbol
        var globalSymbols = new HashSet<string>
        {
            "console", "window", "document", "global", "process",
            "Array", "Object", "String", "Number", "Boolean",
            "Date", "Math", "JSON", "Promise", "Map", "Set",
            "undefined", "null", "true", "false"
        };
        
        return globalSymbols.Contains(symbol);
    }

    private static bool IsImportedSymbol(string symbol, List<ImportInfo> imports)
    {
        foreach (var import in imports)
        {
            if (import.DefaultImport == symbol)
                return true;
                
            if (import.NamedImports?.Any(ni => ni.Name == symbol || ni.Alias == symbol) == true)
                return true;
        }
        
        return false;
    }
}

// Context Preservation Models
public class ContextPreservationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    public ScopeAnalysis? ScopeAnalysis { get; set; }
    public MethodAstInfo? MethodInfo { get; set; }
    
    public bool RequiresThisContext { get; set; }
    public string? ThisContextType { get; set; }
    public string? ParentClassName { get; set; }
    public List<ThisReference> ThisReferences { get; set; } = [];
    public List<AccessedMember> AccessedClassMembers { get; set; } = [];
    
    public bool RequiresClosureVariables { get; set; }
    public List<string> ClosureVariables { get; set; } = [];
    
    public List<RequiredParameter> RequiredParameters { get; set; } = [];
    public List<string> PreservationStrategies { get; set; } = [];
    public ExtractionGuidance ExtractionGuidance { get; set; } = new();
}

public class AccessedMember
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public bool IsProtected { get; set; }
    public bool IsStatic { get; set; }
}

public class RequiredParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "any";
    public bool IsModified { get; set; }
    public string Source { get; set; } = string.Empty; // "closure", "external", "this"
}

public class ExtractionGuidance
{
    public TypeScriptFunctionType RecommendedFunctionType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];
}
