using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Validation;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.CSharp;

/// <summary>
/// Service responsible for generating method call statements for extracted methods
/// Handles variable declarations, assignments, and tuple destructuring
/// EXTRACTED FROM: CSharpMethodExtractor as part of Phase 4 refactoring
/// SESSION 1 FIX: Simplified variable handling logic to eliminate syntax errors
/// SESSION 2 FIX: Fixed parameter passing to ensure clean parameter names without types
/// </summary>
public class MethodCallGenerationService : IMethodCallGenerationService
{
    private readonly IEnhancedVariableAnalysisService _enhancedVariableAnalysis;
    private readonly ILogger<MethodCallGenerationService>? _logger;

    public MethodCallGenerationService(
        IEnhancedVariableAnalysisService enhancedVariableAnalysis,
        ILogger<MethodCallGenerationService>? logger = null)
    {
        _enhancedVariableAnalysis = enhancedVariableAnalysis ?? throw new ArgumentNullException(nameof(enhancedVariableAnalysis));
        _logger = logger;
    }

    /// <summary>
    /// Creates a method call statement based on validation analysis results
    /// SESSION 1 FIX: Simplified logic to properly handle variable declarations and assignments
    /// SESSION 2 FIX: Ensure clean parameter passing without type information
    /// </summary>
    public async Task<string> CreateMethodCallAsync(string indentation, string methodName, MethodExtractionValidationResult validationResult, string[] extractedLines)
    {
        _logger?.LogDebug("Creating method call for {MethodName}", methodName);

        // Perform enhanced variable analysis
        var enhancedAnalysisTask = _enhancedVariableAnalysis.PerformCompleteAnalysisAsync(
            extractedLines,
            null, // SemanticModel will be created internally if needed
            []
        );
        
        var enhancedAnalysis = await enhancedAnalysisTask;
        
        if (!enhancedAnalysis.IsSuccessful)
        {
            _logger?.LogWarning("Enhanced analysis failed: {Errors}", 
                string.Join(", ", enhancedAnalysis.Errors));
            // Fall back to a simple method call
            return GenerateSimpleMethodCall(indentation, methodName, validationResult);
        }

        var mapping = enhancedAnalysis.HandlingMapping;
        
        // Build parameters list
        var parameters = BuildParametersList(mapping, validationResult);
        
        // If no return values, it's a void method
        if (mapping.VariablesToReturn.Count == 0 || mapping.SuggestedReturnType == "void")
        {
            _logger?.LogDebug("Generating void method call");
            return $"{indentation}{methodName}({parameters});";
        }
        
        // Determine which variables need declaration vs. assignment
        var variablesToDeclare = mapping.VariablesToDeclare
            .Where(v => mapping.VariablesToReturn.Any(r => r.Name == v.Name))
            .ToList();
            
        var variablesToAssign = mapping.VariablesToAssign
            .Where(v => mapping.VariablesToReturn.Any(r => r.Name == v.Name))
            .ToList();
        
        _logger?.LogDebug("Variables to return: {Return}, to declare: {Declare}, to assign: {Assign}",
            string.Join(", ", mapping.VariablesToReturn.Select(v => v.Name)),
            string.Join(", ", variablesToDeclare.Select(v => v.Name)),
            string.Join(", ", variablesToAssign.Select(v => v.Name)));
        
        // Case 1: All return variables need declaration (local variables)
        if (variablesToDeclare.Count > 0 && variablesToAssign.Count == 0)
        {
            return GenerateDeclarationCall(indentation, methodName, parameters, mapping.VariablesToReturn);
        }
        
        // Case 2: All return variables need assignment (external modified variables)
        if (variablesToAssign.Count > 0 && variablesToDeclare.Count == 0)
        {
            return GenerateAssignmentCall(indentation, methodName, parameters, mapping.VariablesToReturn);
        }
        
        // Case 3: Mixed - some need declaration, some need assignment
        // This is complex and likely indicates a problem with the extraction
        if (variablesToDeclare.Count > 0 && variablesToAssign.Count > 0)
        {
            _logger?.LogWarning("Mixed declaration/assignment scenario - using assignment for all");
            // Default to assignment since external variables can't be redeclared
            return GenerateAssignmentCall(indentation, methodName, parameters, mapping.VariablesToReturn);
        }

        if (RequiresTupleDestructuring(enhancedAnalysis))
        {
            return GenerateTupleDestructuringCall(methodName, enhancedAnalysis.ModifiedVariables, parameters.Split(',').ToList());
        }
        
        // Case 4: Return values but no specific declaration/assignment needed
        // This shouldn't happen with proper analysis, but provide fallback
        _logger?.LogWarning("Unexpected state: return values without clear declaration/assignment");
        return GenerateDeclarationCall(indentation, methodName, parameters, mapping.VariablesToReturn);
    }
    
    /// <summary>
    /// Extracts variable names for tuple return destructuring
    /// </summary>
    public async Task<string> ExtractTupleVariableNamesAsync(string[] extractedLines, string tupleType)
    {
        // Perform analysis to find variables that need to be returned
        var analysisTask = _enhancedVariableAnalysis.PerformCompleteAnalysisAsync(
            extractedLines,
            null,
            []
        );
        
        var analysis = await analysisTask;
        
        if (analysis is { IsSuccessful: true, HandlingMapping.VariablesToReturn.Count: > 0 })
        {
            // Return comma-separated variable names
            return string.Join(", ", analysis.HandlingMapping.VariablesToReturn.Select(v => v.Name));
        }
        
        // Fallback: generate generic names based on tuple type
        var variableNames = new List<string>();
        
        // Remove parentheses and split by comma
        var inner = tupleType.Trim('(', ')');
        var parts = inner.Split(',');
        
        for (var i = 0; i < parts.Length; i++)
        {
            variableNames.Add($"item{i + 1}");
        }
        
        return string.Join(", ", variableNames);
    }

    /// <summary>
    /// Finds the main variable that should capture the return value
    /// </summary>
    public async Task<string?> FindMainVariableAsync(string[] extractedLines, string returnType)
    {
        // Perform analysis to find the main return variable
        var analysisTask = _enhancedVariableAnalysis.PerformCompleteAnalysisAsync(
            extractedLines,
            null,
            []
        );
        
        var analysis = await analysisTask;
        
        if (analysis is { IsSuccessful: true, HandlingMapping.VariablesToReturn.Count: > 0 })
        {
            // Return the first variable that needs to be returned
            return analysis.HandlingMapping.VariablesToReturn.First().Name;
        }
        
        // Fallback: look for common patterns
        var lines = string.Join(" ", extractedLines);
        
        // Look for "result" variable
        if (lines.Contains("result"))
            return "result";
            
        // Look for any variable declaration that matches the return type
        if (!string.IsNullOrEmpty(returnType) && returnType != "void")
        {
            // Simple pattern matching for variable declarations
            var pattern = $@"\b{returnType}\s+(\w+)\s*=";
            var match = Regex.Match(lines, pattern);
            if (match.Success)
                return match.Groups[1].Value;
        }
        
        return null;
    }

    /// <summary>
    /// Determines if a string is a C# keyword or static member
    /// </summary>
    public bool IsKeywordOrStaticMember(string name)
    {
        // Check for C# keywords
        string[] keywords =
        [
            "int", "string", "bool", "double", "float", "decimal", "char", "byte", 
                             "short", "long", "object", "var", "void", "class", "struct", "interface",
                             "public", "private", "protected", "internal", "static", "const", "readonly",
                             "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
                             "break", "continue", "return", "throw", "try", "catch", "finally", "using",
                             "namespace", "this", "base", "new", "true", "false", "null"
        ];
        
        if (keywords.Contains(name.ToLower()))
            return true;
            
        // Check for common static members
        string[] staticMembers =
        [
            "Console", "Math", "DateTime", "String", "Convert", "Environment",
                                  "File", "Directory", "Path", "Debug", "Trace"
        ];
        
        return staticMembers.Contains(name);
    }
    
    #region Private Helper Methods
    
    /// <summary>
    /// Generate method call with variable declarations (for local variables)
    /// </summary>
    private string GenerateDeclarationCall(string indentation, string methodName, string parameters, List<VariableInfo> returnVars)
    {
        if (returnVars.Count == 1)
        {
            // Single variable: var x = Method();
            var varName = returnVars[0].Name;
            _logger?.LogDebug("Generating single variable declaration: var {Name} = {Method}()", varName, methodName);
            return $"{indentation}var {varName} = {methodName}({parameters});";
        }
        else
        {
            // Multiple variables: var (x, y, z) = Method();
            var varNames = string.Join(", ", returnVars.Select(v => v.Name));
            _logger?.LogDebug("Generating tuple declaration: var ({Names}) = {Method}()", varNames, methodName);
            return $"{indentation}var ({varNames}) = {methodName}({parameters});";
        }
    }
    
    /// <summary>
    /// Generate method call with variable assignments (for external modified variables)
    /// </summary>
    private string GenerateAssignmentCall(string indentation, string methodName, string parameters, List<VariableInfo> returnVars)
    {
        if (returnVars.Count == 1)
        {
            // Single variable: x = Method();
            var varName = returnVars[0].Name;
            _logger?.LogDebug("Generating single variable assignment: {Name} = {Method}()", varName, methodName);
            return $"{indentation}{varName} = {methodName}({parameters});";
        }
        else
        {
            // Multiple variables: (x, y, z) = Method();
            var varNames = string.Join(", ", returnVars.Select(v => v.Name));
            _logger?.LogDebug("Generating tuple assignment: ({Names}) = {Method}()", varNames, methodName);
            return $"{indentation}({varNames}) = {methodName}({parameters});";
        }
    }
    
    /// <summary>
    /// Build the parameters list for the method call
    /// SESSION 2 FIX: Ensure clean parameter names without type information
    /// </summary>
    private string BuildParametersList(VariableHandlingMapping mapping, MethodExtractionValidationResult validationResult)
    {
        // SESSION 2 FIX: Priority 1 - Use parameters from enhanced analysis
        if (mapping.ParametersToPass?.Count > 0)
        {
            // Clean parameter names - ensure no type information
            var cleanParams = mapping.ParametersToPass
                .Select(p => CleanParameterName(p.Name))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
                
            _logger?.LogDebug("Enhanced analysis parameters (cleaned): {Params}", 
                string.Join(", ", cleanParams));
            
            if (cleanParams.Count > 0)
                return string.Join(", ", cleanParams);
        }
        
        // SESSION 2 FIX: Priority 2 - Use validation result parameters but clean them
        if (validationResult.Analysis?.SuggestedParameters?.Count > 0)
        {
            var cleanParams = validationResult.Analysis.SuggestedParameters
                .Select(p => CleanParameterName(p))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
                
            _logger?.LogDebug("Validation result parameters (cleaned): {Params}", 
                string.Join(", ", cleanParams));
                
            if (cleanParams.Count > 0)
                return string.Join(", ", cleanParams);
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// SESSION 2 FIX: Clean parameter name to remove any type information
    /// </summary>
    private string CleanParameterName(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            return string.Empty;
            
        // Remove any type declarations (e.g., "int x" -> "x")
        // Pattern: type name or just name
        var typePattern = @"^(?:\w+(?:\<[^>]+\>)?(?:\[\])?(?:\?)?)\s+(\w+)$";
        var match = Regex.Match(parameterName.Trim(), typePattern);
        
        if (match.Success)
        {
            var cleanName = match.Groups[1].Value;
            _logger?.LogDebug("Cleaned parameter: '{Original}' -> '{Clean}'", parameterName, cleanName);
            return cleanName;
        }
        
        // Check if it's already just a name (no spaces)
        if (!parameterName.Contains(' '))
        {
            // But make sure it's not a keyword
            if (!IsKeywordOrStaticMember(parameterName))
            {
                return parameterName.Trim();
            }
            else
            {
                _logger?.LogDebug("Parameter '{Name}' is a keyword/static member - skipping", parameterName);
                return string.Empty;
            }
        }
        
        // If there are spaces but didn't match the pattern, take the last word
        // This handles cases like "ref int x" -> "x"
        var parts = parameterName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            var lastPart = parts[^1];
            if (!IsKeywordOrStaticMember(lastPart))
            {
                _logger?.LogDebug("Using last part as parameter: '{Original}' -> '{Clean}'", 
                    parameterName, lastPart);
                return lastPart;
            }
        }
        
        _logger?.LogWarning("Could not clean parameter name: '{Name}'", parameterName);
        return string.Empty;
    }
    
    /// <summary>
    /// Generate a simple method call when enhanced analysis fails
    /// SESSION 2 FIX: Clean parameters in fallback scenario too
    /// </summary>
    private string GenerateSimpleMethodCall(string indentation, string methodName, MethodExtractionValidationResult validationResult)
    {
        _logger?.LogDebug("Generating simple fallback method call");
        
        // Build parameters from validation result - SESSION 2 FIX: Clean them
        var parameters = "";
        if (validationResult.Analysis?.SuggestedParameters?.Count > 0)
        {
            var cleanParams = validationResult.Analysis.SuggestedParameters
                .Select(p => CleanParameterName(p))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
                
            parameters = string.Join(", ", cleanParams);
        }
        
        // Check if we need a return value
        if (validationResult.Analysis?.RequiresReturnValue == true)
        {
            var returnType = validationResult.Analysis.SuggestedReturnType;
            
            // If we have modified variables, use them for assignment
            if (validationResult.Analysis.ModifiedVariables?.Count > 0)
            {
                if (validationResult.Analysis.ModifiedVariables.Count == 1)
                {
                    return $"{indentation}{validationResult.Analysis.ModifiedVariables[0]} = {methodName}({parameters});";
                }
                else
                {
                    var vars = string.Join(", ", validationResult.Analysis.ModifiedVariables);
                    return $"{indentation}({vars}) = {methodName}({parameters});";
                }
            }
            
            // If we have local variables, declare them
            if (validationResult.Analysis.LocalVariables?.Count > 0)
            {
                if (validationResult.Analysis.LocalVariables.Count == 1)
                {
                    return $"{indentation}var {validationResult.Analysis.LocalVariables[0]} = {methodName}({parameters});";
                }
                else
                {
                    var vars = string.Join(", ", validationResult.Analysis.LocalVariables);
                    return $"{indentation}var ({vars}) = {methodName}({parameters});";
                }
            }
            
            // Default: use generic result variable
            return $"{indentation}var result = {methodName}({parameters});";
        }
        
        // Void method call
        return $"{indentation}{methodName}({parameters});";
    }

    private static bool RequiresTupleDestructuring(EnhancedVariableAnalysisResult analysis)
    {
        return analysis.ModifiedVariables.Count > 1;
    }
    
    private static string GenerateTupleDestructuringCall(string methodName, 
        List<VariableInfo> modifiedVariables, 
        List<string> parameters)
    {
        var variableNames = modifiedVariables.Select(v => v.Name).ToList();
        var parameterList = string.Join(", ", parameters);
    
        return $"({string.Join(", ", variableNames)}) = {methodName}({parameterList});";
    }
    
    #endregion
}
