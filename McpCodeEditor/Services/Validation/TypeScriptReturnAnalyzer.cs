using McpCodeEditor.Models.TypeScript;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace McpCodeEditor.Services.Validation;

/// <summary>
/// Advanced TypeScript return value analysis for sophisticated method extraction
/// Provides detailed analysis of return values, variable flows, and type inference
/// </summary>
public class TypeScriptReturnAnalyzer(ILogger<TypeScriptReturnAnalyzer> logger)
{
    // Sophisticated patterns for TypeScript analysis
    private static readonly Regex ReturnStatementPattern = new(@"^\s*return\s+(.*?);?\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex VariableAssignmentPattern = new(@"^\s*(\w+)\s*=\s*(.+);?\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex ModificationPattern = new(@"(\w+)\.(\w+)\s*=|(\w+)\[.+\]\s*=|(\w+)\+\+|(\w+)--|(\w+)\s*\+=|(\w+)\s*-=", RegexOptions.Compiled);
    private static readonly Regex ObjectDestructuringPattern = new(@"const\s*{\s*([^}]+)\s*}\s*=", RegexOptions.Compiled);
    private static readonly Regex ArrayDestructuringPattern = new(@"const\s*\[\s*([^\]]+)\s*\]\s*=", RegexOptions.Compiled);
    private static readonly Regex FunctionCallPattern = new(@"(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex AsyncCallPattern = new(@"await\s+([^;]+)", RegexOptions.Compiled);
    private static readonly Regex ConditionalPattern = new(@"if\s*\([^)]+\)\s*{([^}]+)}", RegexOptions.Compiled | RegexOptions.Singleline);
    
    /// <summary>
    /// Perform comprehensive return value analysis for TypeScript method extraction
    /// </summary>
    public async Task<TypeScriptReturnAnalysisResult> AnalyzeReturnValuesAsync(
        string extractedCode,
        string fullSourceCode,
        TypeScriptExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new TypeScriptReturnAnalysisResult();
            
            logger.LogDebug("Starting comprehensive TypeScript return analysis for method: {MethodName}", 
                options.NewMethodName);

            // Step 1: Analyze explicit return statements
            await AnalyzeExplicitReturnsAsync(extractedCode, result, cancellationToken);
            
            // Step 2: Analyze variable modifications and mutations
            await AnalyzeVariableModificationsAsync(extractedCode, fullSourceCode, result, cancellationToken);
            
            // Step 3: Analyze complex expressions and assignments
            await AnalyzeComplexExpressionsAsync(extractedCode, result, cancellationToken);
            
            // Step 4: Analyze control flow and conditional returns
            await AnalyzeControlFlowReturnsAsync(extractedCode, result, cancellationToken);
            
            // Step 5: Infer optimal return strategy
            await InferOptimalReturnStrategyAsync(extractedCode, result, options, cancellationToken);
            
            // Step 6: Generate return type suggestions
            await GenerateReturnTypeSuggestionsAsync(result, cancellationToken);

            logger.LogDebug("TypeScript return analysis completed. Return paths: {ReturnPathCount}, Variables: {VariableCount}", 
                result.ReturnPaths.Count, result.VariablesToReturn.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during TypeScript return value analysis");
            return new TypeScriptReturnAnalysisResult
            {
                Success = false,
                ErrorMessage = $"Return analysis failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Analyze explicit return statements in the code
    /// </summary>
    private static async Task AnalyzeExplicitReturnsAsync(
        string extractedCode, 
        TypeScriptReturnAnalysisResult result, 
        CancellationToken cancellationToken)
    {
        MatchCollection returnMatches = ReturnStatementPattern.Matches(extractedCode);
        
        foreach (Match match in returnMatches)
        {
            var returnPath = new TypeScriptReturnPath
            {
                Type = TypeScriptReturnType.ExplicitReturn,
                Expression = match.Groups[1].Value.Trim(),
                LineNumber = GetLineNumber(extractedCode, match.Index)
            };

            // Analyze the return expression
            await AnalyzeReturnExpressionAsync(returnPath.Expression, returnPath, cancellationToken);
            result.ReturnPaths.Add(returnPath);
        }

        result.HasExplicitReturns = returnMatches.Count > 0;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Analyze variable modifications that might need to be returned
    /// </summary>
    private static async Task AnalyzeVariableModificationsAsync(
        string extractedCode, 
        string fullSourceCode, 
        TypeScriptReturnAnalysisResult result, 
        CancellationToken cancellationToken)
    {
        // Find variables that are modified in the extracted code
        MatchCollection modificationMatches = ModificationPattern.Matches(extractedCode);
        var modifiedVariables = new HashSet<string>();

        foreach (Match match in modificationMatches)
        {
            // Extract variable name from different modification patterns
            for (var i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success && !string.IsNullOrEmpty(match.Groups[i].Value))
                {
                    modifiedVariables.Add(match.Groups[i].Value);
                    break;
                }
            }
        }

        // Check if these variables are used outside the extracted code
        foreach (string variable in modifiedVariables)
        {
            if (await IsVariableUsedAfterExtractionAsync(variable, fullSourceCode, cancellationToken))
            {
                result.VariablesToReturn.Add(new TypeScriptVariableReturn
                {
                    VariableName = variable,
                    IsModified = true,
                    InferredType = await InferVariableTypeAsync(variable, extractedCode, cancellationToken),
                    RequiredForReturn = true
                });
            }
        }
    }

    /// <summary>
    /// Analyze complex expressions, assignments, and object manipulations
    /// </summary>
    private async Task AnalyzeComplexExpressionsAsync(
        string extractedCode, 
        TypeScriptReturnAnalysisResult result, 
        CancellationToken cancellationToken)
    {
        // Analyze variable assignments
        MatchCollection assignmentMatches = VariableAssignmentPattern.Matches(extractedCode);
        
        foreach (Match match in assignmentMatches)
        {
            string variableName = match.Groups[1].Value;
            string expression = match.Groups[2].Value;

            var variableReturn = new TypeScriptVariableReturn
            {
                VariableName = variableName,
                AssignedExpression = expression,
                InferredType = await InferExpressionTypeAsync(expression, cancellationToken),
                IsModified = true
            };

            result.VariablesToReturn.Add(variableReturn);
        }

        // Analyze object destructuring
        await AnalyzeDestructuringPatternsAsync(extractedCode, result, cancellationToken);
        
        // Analyze async operations
        await AnalyzeAsyncOperationsAsync(extractedCode, result, cancellationToken);
    }

    /// <summary>
    /// Analyze control flow and conditional returns
    /// </summary>
    private static async Task AnalyzeControlFlowReturnsAsync(
        string extractedCode, 
        TypeScriptReturnAnalysisResult result, 
        CancellationToken cancellationToken)
    {
        MatchCollection conditionalMatches = ConditionalPattern.Matches(extractedCode);
        
        foreach (Match match in conditionalMatches)
        {
            string conditionalCode = match.Groups[1].Value;
            
            // Recursively analyze return statements in conditional blocks
            MatchCollection conditionalReturns = ReturnStatementPattern.Matches(conditionalCode);
            
            foreach (Match returnMatch in conditionalReturns)
            {
                var returnPath = new TypeScriptReturnPath
                {
                    Type = TypeScriptReturnType.ConditionalReturn,
                    Expression = returnMatch.Groups[1].Value.Trim(),
                    IsConditional = true,
                    LineNumber = GetLineNumber(extractedCode, returnMatch.Index)
                };

                await AnalyzeReturnExpressionAsync(returnPath.Expression, returnPath, cancellationToken);
                result.ReturnPaths.Add(returnPath);
            }
        }

        result.HasConditionalReturns = conditionalMatches.Count > 0;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Infer the optimal return strategy based on analysis
    /// </summary>
    private static async Task InferOptimalReturnStrategyAsync(
        string extractedCode,
        TypeScriptReturnAnalysisResult result, 
        TypeScriptExtractionOptions options,
        CancellationToken cancellationToken)
    {
        // Determine the best return strategy
        if (result is { HasExplicitReturns: true, VariablesToReturn.Count: 0 })
        {
            result.OptimalReturnStrategy = TypeScriptReturnStrategy.ExplicitReturn;
            result.InferredReturnType = await InferReturnTypeFromPathsAsync(result.ReturnPaths, cancellationToken);
        }
        else if (result is { HasExplicitReturns: false, VariablesToReturn.Count: 1 })
        {
            result.OptimalReturnStrategy = TypeScriptReturnStrategy.SingleVariable;
            result.InferredReturnType = result.VariablesToReturn[0].InferredType;
        }
        else if (result is { HasExplicitReturns: false, VariablesToReturn.Count: > 1 })
        {
            result.OptimalReturnStrategy = TypeScriptReturnStrategy.MultipleVariables;
            result.InferredReturnType = await GenerateObjectReturnTypeAsync(result.VariablesToReturn, cancellationToken);
        }
        else if (result is { HasExplicitReturns: true, VariablesToReturn.Count: > 0 })
        {
            result.OptimalReturnStrategy = TypeScriptReturnStrategy.MixedReturn;
            result.InferredReturnType = "any"; // Complex mixed return scenario
            result.Warnings.Add("Mixed return patterns detected. Consider simplifying the extracted code.");
        }
        else
        {
            result.OptimalReturnStrategy = TypeScriptReturnStrategy.VoidReturn;
            result.InferredReturnType = "void";
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Generate sophisticated return type suggestions
    /// </summary>
    private static async Task GenerateReturnTypeSuggestionsAsync(
        TypeScriptReturnAnalysisResult result, 
        CancellationToken cancellationToken)
    {
        var suggestions = new List<string>();

        switch (result.OptimalReturnStrategy)
        {
            case TypeScriptReturnStrategy.ExplicitReturn:
                suggestions.Add($"Use explicit return type: {result.InferredReturnType}");
                break;

            case TypeScriptReturnStrategy.SingleVariable:
                suggestions.Add($"Return single variable: {result.VariablesToReturn[0].VariableName}");
                suggestions.Add($"Suggested return type: {result.InferredReturnType}");
                break;

            case TypeScriptReturnStrategy.MultipleVariables:
                suggestions.Add("Return object with multiple properties:");
                foreach (TypeScriptVariableReturn variable in result.VariablesToReturn)
                {
                    suggestions.Add($"  {variable.VariableName}: {variable.InferredType}");
                }
                break;

            case TypeScriptReturnStrategy.VoidReturn:
                suggestions.Add("Method performs side effects only, no return value needed");
                break;

            case TypeScriptReturnStrategy.MixedReturn:
                suggestions.Add("Complex return pattern detected");
                suggestions.Add("Consider refactoring to simplify return logic");
                break;
        }

        result.Suggestions.AddRange(suggestions);
        await Task.CompletedTask;
    }

    #region Helper Methods

    private static async Task AnalyzeReturnExpressionAsync(string expression, TypeScriptReturnPath returnPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            returnPath.InferredType = "void";
            return;
        }

        returnPath.InferredType = await InferExpressionTypeAsync(expression, cancellationToken);
        
        // Check for complex expressions
        if (expression.Contains("await"))
        {
            returnPath.IsAsync = true;
        }
        
        if (expression.Contains("{") && expression.Contains("}"))
        {
            returnPath.IsObjectLiteral = true;
        }
        
        if (expression.Contains("[") && expression.Contains("]"))
        {
            returnPath.IsArrayLiteral = true;
        }
    }

    private static async Task<string> InferExpressionTypeAsync(string expression, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async operations
        
        if (string.IsNullOrWhiteSpace(expression))
            return "void";

        // Basic type inference patterns
        if (Regex.IsMatch(expression, @"^\d+$"))
            return "number";
            
        if (Regex.IsMatch(expression, @"^\d*\.\d+$"))
            return "number";
            
        if (Regex.IsMatch(expression, @"^(true|false)$"))
            return "boolean";
            
        if (Regex.IsMatch(expression, @"^['""`].*['""`]$"))
            return "string";
            
        if (expression.Trim().StartsWith("{") && expression.Trim().EndsWith("}"))
            return "object";
            
        if (expression.Trim().StartsWith("[") && expression.Trim().EndsWith("]"))
            return "array";
            
        if (expression.Contains("new "))
            return ExtractConstructorType(expression);
            
        if (expression.Contains("await "))
            return "Promise<any>";

        return "any"; // Default fallback
    }

    private static async Task<string> InferVariableTypeAsync(string variableName, string context, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async operations
        
        // Look for type declarations or assignments
        var typePattern = new Regex($@"{variableName}\s*:\s*(\w+)", RegexOptions.IgnoreCase);
        Match match = typePattern.Match(context);
        
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Look for assignments to infer type
        var assignmentPattern = new Regex($@"{variableName}\s*=\s*(.+?)[;\n]", RegexOptions.IgnoreCase);
        Match assignmentMatch = assignmentPattern.Match(context);
        
        if (assignmentMatch.Success)
        {
            return await InferExpressionTypeAsync(assignmentMatch.Groups[1].Value, cancellationToken);
        }

        return "any";
    }

    private static async Task<bool> IsVariableUsedAfterExtractionAsync(string variableName, string fullSourceCode, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for more sophisticated analysis
        
        // Simple heuristic: check if variable is referenced after the extraction point
        // This would need more sophisticated analysis in a real implementation
        return fullSourceCode.Contains(variableName);
    }

    private static async Task AnalyzeDestructuringPatternsAsync(string extractedCode, TypeScriptReturnAnalysisResult result, CancellationToken cancellationToken)
    {
        // Analyze object destructuring
        MatchCollection objectMatches = ObjectDestructuringPattern.Matches(extractedCode);
        foreach (Match match in objectMatches)
        {
            IEnumerable<string> variables = match.Groups[1].Value.Split(',').Select(v => v.Trim());
            foreach (string variable in variables)
            {
                result.VariablesToReturn.Add(new TypeScriptVariableReturn
                {
                    VariableName = variable,
                    IsDestructured = true,
                    InferredType = await InferVariableTypeAsync(variable, extractedCode, cancellationToken)
                });
            }
        }

        // Analyze array destructuring  
        MatchCollection arrayMatches = ArrayDestructuringPattern.Matches(extractedCode);
        foreach (Match match in arrayMatches)
        {
            IEnumerable<string> variables = match.Groups[1].Value.Split(',').Select(v => v.Trim());
            foreach (string variable in variables)
            {
                result.VariablesToReturn.Add(new TypeScriptVariableReturn
                {
                    VariableName = variable,
                    IsDestructured = true,
                    InferredType = await InferVariableTypeAsync(variable, extractedCode, cancellationToken)
                });
            }
        }
    }

    private static async Task AnalyzeAsyncOperationsAsync(string extractedCode, TypeScriptReturnAnalysisResult result, CancellationToken cancellationToken)
    {
        MatchCollection asyncMatches = AsyncCallPattern.Matches(extractedCode);
        
        if (asyncMatches.Count > 0)
        {
            result.HasAsyncOperations = true;
            
            foreach (Match match in asyncMatches)
            {
                var returnPath = new TypeScriptReturnPath
                {
                    Type = TypeScriptReturnType.AsyncReturn,
                    Expression = match.Groups[1].Value.Trim(),
                    IsAsync = true,
                    InferredType = "Promise<any>"
                };
                
                result.ReturnPaths.Add(returnPath);
            }
        }

        await Task.CompletedTask;
    }

    private static async Task<string> InferReturnTypeFromPathsAsync(List<TypeScriptReturnPath> returnPaths, CancellationToken cancellationToken)
    {
        if (returnPaths.Count == 0)
            return "void";

        if (returnPaths.Count == 1)
            return returnPaths[0].InferredType;

        // Multiple return paths - check if they're compatible
        List<string> types = returnPaths.Select(p => p.InferredType).Distinct().ToList();
        
        if (types.Count == 1)
        {
            return types[0];
        }
        
        // Multiple different types - use union type or any
        if (types.All(t => t != "any"))
        {
            return string.Join(" | ", types);
        }

        await Task.CompletedTask;
        return "any";
    }

    private static async Task<string> GenerateObjectReturnTypeAsync(List<TypeScriptVariableReturn> variables, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        IEnumerable<string> properties = variables.Select(v => $"{v.VariableName}: {v.InferredType}");
        return $"{{ {string.Join(", ", properties)} }}";
    }

    private static string ExtractConstructorType(string expression)
    {
        var newPattern = new Regex(@"new\s+(\w+)", RegexOptions.IgnoreCase);
        Match match = newPattern.Match(expression);
        return match.Success ? match.Groups[1].Value : "object";
    }

    private static int GetLineNumber(string text, int index)
    {
        return text.Take(index).Count(c => c == '\n') + 1;
    }

    #endregion
}

#region Enhanced Data Models

/// <summary>
/// Comprehensive result of TypeScript return value analysis
/// </summary>
public class TypeScriptReturnAnalysisResult
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public bool HasExplicitReturns { get; set; }
    public bool HasConditionalReturns { get; set; }
    public bool HasAsyncOperations { get; set; }
    public TypeScriptReturnStrategy OptimalReturnStrategy { get; set; }
    public string InferredReturnType { get; set; } = "void";
    public List<TypeScriptReturnPath> ReturnPaths { get; set; } = [];
    public List<TypeScriptVariableReturn> VariablesToReturn { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Represents a single return path in the code
/// </summary>
public class TypeScriptReturnPath
{
    public TypeScriptReturnType Type { get; set; }
    public string Expression { get; set; } = string.Empty;
    public string InferredType { get; set; } = "any";
    public bool IsConditional { get; set; }
    public bool IsAsync { get; set; }
    public bool IsObjectLiteral { get; set; }
    public bool IsArrayLiteral { get; set; }
    public int LineNumber { get; set; }
}

/// <summary>
/// Represents a variable that needs to be returned
/// </summary>
public class TypeScriptVariableReturn
{
    public string VariableName { get; set; } = string.Empty;
    public string InferredType { get; set; } = "any";
    public bool IsModified { get; set; }
    public bool IsDestructured { get; set; }
    public bool RequiredForReturn { get; set; }
    public string? AssignedExpression { get; set; }
}

/// <summary>
/// Types of return statements
/// </summary>
public enum TypeScriptReturnType
{
    ExplicitReturn,
    ConditionalReturn,
    AsyncReturn,
    ImplicitReturn
}

/// <summary>
/// Strategies for handling return values
/// </summary>
public enum TypeScriptReturnStrategy
{
    VoidReturn,
    ExplicitReturn,
    SingleVariable,
    MultipleVariables,
    MixedReturn
}

#endregion
