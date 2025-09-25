using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Validation;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using McpCodeEditor.Models.Refactoring.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpCodeEditor.Services.CSharp;

/// <summary>
/// Service for generating C# method signatures during method extraction
/// Handles parameter generation, return types, and method body formatting
/// PHASE 2: Enhanced Variable Analysis Integration
/// SESSION 3: Fixed parameter type handling and improved type inference
/// </summary>
public class MethodSignatureGenerationService(
    IMethodCallGenerationService methodCallGenerationService,
    IEnhancedVariableAnalysisService enhancedVariableAnalysisService,
    ILogger<MethodSignatureGenerationService>? logger = null)
    : IMethodSignatureGenerationService
{
    private readonly IMethodCallGenerationService _methodCallGenerationService = methodCallGenerationService ?? throw new ArgumentNullException(nameof(methodCallGenerationService));
    private readonly IEnhancedVariableAnalysisService _enhancedVariableAnalysisService = enhancedVariableAnalysisService ?? throw new ArgumentNullException(nameof(enhancedVariableAnalysisService));

    /// <summary>
    /// Creates an extracted method with parameters based on enhanced semantic analysis
    /// SESSION 3: Improved parameter type handling and return type generation
    /// </summary>
    public async Task<string> CreateExtractedMethodWithParametersAsync(
        string[] extractedLines,
        CSharpExtractionOptions options, 
        string returnType, 
        string baseIndentation,
        MethodExtractionValidationResult validationResult)
    {
        logger?.LogDebug("CreateExtractedMethodWithParameters called (Session 3 Enhanced):");
        logger?.LogDebug("  returnType parameter: {ReturnType}", returnType);
        logger?.LogDebug("  options.ReturnType: {OptionsReturnType}", options.ReturnType);

        // Calculate the base line indentation for maintaining relative spacing
        string? baseLineIndentation = null;
        foreach (var line in extractedLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                baseLineIndentation = GetLineIndentation(line);
                break;
            }
        }

        baseLineIndentation ??= "";

        logger?.LogDebug("  Base line indentation: '{BaseIndent}' (length: {Length})", 
            baseLineIndentation, baseLineIndentation.Length);

        // Determine return requirements using enhanced analysis
        var returnInfo = await DetermineReturnRequirementsUsingEnhancedAnalysisAsync(extractedLines, validationResult, returnType);

        logger?.LogDebug("  Enhanced analysis complete: needsReturn={NeedsReturn}, finalType={FinalType}", 
            returnInfo.NeedsReturn, returnInfo.FinalReturnType);

        // Build method body with actual newlines
        var methodBody = string.Join(Environment.NewLine, extractedLines.Select(line =>
        {
            // Remove existing indentation and apply new base method indentation
            var trimmedLine = line.TrimStart();
            if (string.IsNullOrWhiteSpace(trimmedLine))
                return "        " + trimmedLine; // Empty line gets base method indentation

            // Calculate original relative indentation
            var originalIndentation = GetLineIndentation(line);
            var relativeSpaces = Math.Max(0, originalIndentation.Length - baseLineIndentation.Length);
            var newIndentation = "        " + new string(' ', relativeSpaces);

            return newIndentation + trimmedLine;
        }));

        // Add a return statement if needed with proper syntax
        if (returnInfo.NeedsReturn && !string.IsNullOrEmpty(returnInfo.VariablesToReturn))
        {
            // Ensure the return statement doesn't have double parentheses for tuples
            var returnStatement = returnInfo.VariablesToReturn;
            if (returnStatement.StartsWith("((") && returnStatement.EndsWith("))"))
            {
                // Remove outer parentheses if double-wrapped
                returnStatement = returnStatement.Substring(1, returnStatement.Length - 2);
            }
            
            methodBody += Environment.NewLine + "        return " + returnStatement + ";";
            logger?.LogDebug("  Return statement added: {ReturnStatement}", returnStatement);
        }
        else
        {
            methodBody = methodBody.TrimEnd('\n');
            logger?.LogDebug("  No return statement added. needsReturn={NeedsReturn}, variables={Variables}", 
                returnInfo.NeedsReturn, returnInfo.VariablesToReturn ?? "null");
        }

        // Build method signature with parameters from enhanced analysis
        var accessibility = options.AccessModifier ?? "private";
        var staticModifier = options.IsStatic ? " static" : "";
        
        // Use the corrected return type from enhanced analysis
        var finalReturnType = returnInfo.FinalReturnType;
        
        // Build parameter list with proper type handling
        var parameters = BuildParameterListFromEnhancedAnalysis(returnInfo.EnhancedAnalysis);

        var method = $"\n    {accessibility}{staticModifier} {finalReturnType} {options.NewMethodName}({parameters})\n    {{\n{methodBody}\n    }}";

        logger?.LogDebug("  Final method signature: {Signature}", 
            $"{accessibility}{staticModifier} {finalReturnType} {options.NewMethodName}({parameters})");
        
        return method;
    }

    /// <summary>
    /// Finds the variables that should be returned as a tuple (interface implementation)
    /// SESSION 3: Improved tuple variable detection
    /// </summary>
    public async Task<string> FindTupleReturnVariablesAsync(string[] extractedLines, string tupleType)
    {
        logger?.LogDebug("FindTupleReturnVariables called with tupleType: {TupleType}", tupleType);
        
        try
        {
            // Use enhanced variable analysis to determine return variables
            (var semanticModel, var syntaxNodes) = CreateSemanticModelForAnalysis(extractedLines);
            
            var enhancedAnalysisTask = _enhancedVariableAnalysisService.PerformCompleteAnalysisAsync(
                extractedLines, 
                semanticModel,
                syntaxNodes
            );
            
            var enhancedAnalysis = await enhancedAnalysisTask;
            
            if (enhancedAnalysis is { IsSuccessful: true, HandlingMapping.VariablesToReturn.Count: > 0 })
            {
                var variableNames = enhancedAnalysis.HandlingMapping.VariablesToReturn.Select(v => v.Name);
                var result = string.Join(", ", variableNames);
                
                logger?.LogDebug("Enhanced analysis found tuple variables: {Variables}", result);
                return result;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Enhanced analysis failed in FindTupleReturnVariables, using fallback");
        }
        
        // Fallback: Parse tuple type to extract variable names from extractedLines
        var variables = new List<string>();
        
        foreach (var line in extractedLines)
        {
            // Simple pattern to find variable assignments
            var matches = Regex.Matches(line.Trim(), @"\b(\w+)\s*[+\-*/]?=");
            foreach (Match match in matches)
            {
                var varName = match.Groups[1].Value;
                if (!variables.Contains(varName) && !IsKeyword(varName))
                {
                    variables.Add(varName);
                }
            }
        }
        
        var fallbackResult = string.Join(", ", variables.Take(4)); // Limit to reasonable number
        logger?.LogDebug("Fallback analysis found variables: {Variables}", fallbackResult);
        
        return fallbackResult;
    }

    /// <summary>
    /// Gets the indentation of a line (interface implementation - now public)
    /// </summary>
    public string GetLineIndentation(string line)
    {
        var index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }
        return line[..index];
    }

    /// <summary>
    /// Creates a proper SemanticModel for enhanced analysis
    /// </summary>
    private (SemanticModel?, IEnumerable<SyntaxNode>) CreateSemanticModelForAnalysis(string[] extractedLines)
    {
        try
        {
            // Create a complete C# file context for proper semantic analysis
            var extractedCode = string.Join(Environment.NewLine, extractedLines);
            var completeCode = $@"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TempNamespace
{{
    public class TempClass
    {{
        public void TempMethod()
        {{
{extractedCode}
        }}
    }}
}}";

            logger?.LogDebug("Creating SemanticModel for enhanced analysis with {LineCount} lines", extractedLines.Length);

            // Create syntax tree and semantic model
            var syntaxTree = CSharpSyntaxTree.ParseText(completeCode);
            var compilation = CSharpCompilation.Create("TempAssembly")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();
            
            // Find the method body that contains our extracted code
            var methodBody = root.DescendantNodes().OfType<BlockSyntax>().LastOrDefault();
            var syntaxNodes = methodBody?.ChildNodes() ?? [];

            var nodes = syntaxNodes.ToList();
            logger?.LogDebug("SemanticModel created successfully with {NodeCount} syntax nodes", nodes.Count);
            
            return (semanticModel, nodes);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to create SemanticModel for enhanced analysis, falling back to null");
            return (null, []);
        }
    }

    /// <summary>
    /// Enhanced return requirements determination using Enhanced Variable Analysis
    /// SESSION 3: Improved return type generation for complex scenarios
    /// </summary>
    private async Task<EnhancedReturnRequirements> DetermineReturnRequirementsUsingEnhancedAnalysisAsync(
        string[] extractedLines, 
        MethodExtractionValidationResult validationResult, 
        string originalReturnType)
    {
        var result = new EnhancedReturnRequirements
        {
            NeedsReturn = false,
            FinalReturnType = "void",
            VariablesToReturn = null,
            EnhancedAnalysis = null
        };

        try
        {
            // Create proper SemanticModel
            (var semanticModel, var syntaxNodes) = CreateSemanticModelForAnalysis(extractedLines);

            var nodes = syntaxNodes.ToList();
            logger?.LogDebug("Enhanced analysis using {SemanticModel} with {NodeCount} syntax nodes", 
                semanticModel != null ? "proper SemanticModel" : "null fallback", 
                nodes.Count);

            var enhancedAnalysisTask = _enhancedVariableAnalysisService.PerformCompleteAnalysisAsync(
                extractedLines, 
                semanticModel,
                nodes
            );
            
            var enhancedAnalysis = await enhancedAnalysisTask;
            result.EnhancedAnalysis = enhancedAnalysis;

            if (!enhancedAnalysis.IsSuccessful)
            {
                logger?.LogWarning("Enhanced analysis failed, falling back to original logic. Errors: {Errors}", 
                    string.Join(", ", enhancedAnalysis.Errors));
                result.FinalReturnType = originalReturnType;
                return result;
            }

            var mapping = enhancedAnalysis.HandlingMapping;
            
            // Check if analysis indicates return is needed
            if (mapping.VariablesToReturn.Count > 0 && mapping.SuggestedReturnType != "void")
            {
                result.NeedsReturn = true;
                
                // SESSION 3: Improve return type generation
                if (mapping.VariablesToReturn.Count == 1)
                {
                    // Single return - use the variable's type
                    result.VariablesToReturn = mapping.VariablesToReturn[0].Name;
                    result.FinalReturnType = CleanTypeForSignature(mapping.SuggestedReturnType);
                }
                else if (mapping.VariablesToReturn.Count > 1)
                {
                    // Multiple returns - generate proper tuple syntax
                    var names = mapping.VariablesToReturn.Select(v => v.Name);
                    result.VariablesToReturn = $"({string.Join(", ", names)})";
                    
                    // SESSION 3: Build proper tuple return type
                    var types = mapping.VariablesToReturn
                        .Select(v => CleanTypeForSignature(v.Type ?? "object"))
                        .ToList();
                    
                    result.FinalReturnType = $"({string.Join(", ", types)})";
                }

                logger?.LogDebug("Enhanced analysis determined return needed: type={Type}, variables={Variables}", 
                    result.FinalReturnType, result.VariablesToReturn);
            }
            else
            {
                result.NeedsReturn = false;
                result.FinalReturnType = "void";
                logger?.LogDebug("Enhanced analysis determined no return needed");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Enhanced analysis failed with exception, falling back to original logic");
            result.FinalReturnType = originalReturnType;
        }

        return result;
    }

    /// <summary>
    /// SESSION 3: Build parameter list from Enhanced Variable Analysis results with proper type handling
    /// </summary>
    private string BuildParameterListFromEnhancedAnalysis(EnhancedVariableAnalysisResult? enhancedAnalysis)
    {
        if (enhancedAnalysis?.IsSuccessful != true)
        {
            logger?.LogDebug("No enhanced analysis available, using empty parameter list");
            return string.Empty;
        }

        var parameters = new List<string>();
        var mapping = enhancedAnalysis.HandlingMapping;

        // SESSION 3: Add parameters for external variables with proper type handling
        foreach (var param in mapping.ParametersToPass)
        {
            // SESSION 3 FIX: Use proper type inference and avoid "var" in parameters
            var paramType = DetermineParameterType(param);
            parameters.Add($"{paramType} {param.Name}");
        }

        var parameterList = string.Join(", ", parameters);
        logger?.LogDebug("Session 3 enhanced parameter list: {Parameters}", parameterList);
        
        return parameterList;
    }

    /// <summary>
    /// SESSION 3: Determine proper parameter type, avoiding "var" and handling complex types
    /// </summary>
    private string DetermineParameterType(VariableInfo param)
    {
        // If we have a valid type that's not "unknown" or "var", use it
        if (!string.IsNullOrEmpty(param.Type) && 
            param.Type != "unknown" && 
            param.Type != "var")
        {
            return CleanTypeForSignature(param.Type);
        }

        // SESSION 3: Try to infer type from variable name patterns
        var inferredType = InferTypeFromName(param.Name);
        if (inferredType != "object")
        {
            logger?.LogDebug("Inferred type {Type} for parameter {Name}", inferredType, param.Name);
            return inferredType;
        }

        // SESSION 3: Default to object for unknown types
        logger?.LogDebug("Using object type for parameter {Name} (original type: {OriginalType})", 
            param.Name, param.Type ?? "null");
        return "object";
    }

    private string GenerateTupleReturnType(List<VariableInfo> modifiedVariables)
    {
        var types = modifiedVariables.Select(InferTypeFromVariable).ToList();
        return $"({string.Join(", ", types)})";
    }

    private string InferTypeFromVariable(VariableInfo variable)
    {
        if (!string.IsNullOrEmpty(variable.Type) &&
            variable.Type != "object" &&
            variable.Type != "var")
        {
            return CleanTypeForSignature(variable.Type);
        }
        
        logger?.LogWarning($"Type inference failed for variable {variable.Name}, using object type.");
        return InferTypeFromName(variable.Name);
    }

    private static bool HasMultipleModifiedVariables(EnhancedVariableAnalysisResult analysis)
    {
        return analysis.ModifiedVariables.Count > 1;
    }
    
    private static string GenerateTupleReturnStatement(List<VariableInfo> modifiedVariables)
    {
        var variableNames = modifiedVariables.Select(v => v.Name).ToList();
        return $"({string.Join(", ", variableNames)})";
    }

    /// <summary>
    /// SESSION 3: Clean type strings for use in method signatures
    /// </summary>
    private static string CleanTypeForSignature(string type)
    {
        if (string.IsNullOrEmpty(type))
            return "object";

        // Remove "var" keyword
        if (type == "var" || type == "unknown")
            return "object";

        // Handle nullable types
        type = type.Replace("?", "");

        // Remove extra spaces
        type = Regex.Replace(type, @"\s+", " ").Trim();

        // Handle generic types - ensure proper formatting
        if (type.Contains("<") && type.Contains(">"))
        {
            // Already properly formatted generic
            return type;
        }

        return type;
    }

    /// <summary>
    /// SESSION 3: Infer type from variable name patterns
    /// </summary>
    private static string InferTypeFromName(string name)
    {
        // Common naming patterns for type inference
        var patterns = new Dictionary<string, string>
        {
            // Numeric types
            { @"(?i)(count|index|number|num|size|length|id)$", "int" },
            { @"(?i)(total|sum|amount|price|cost|value)$", "decimal" },
            { @"(?i)(percent|percentage|ratio|rate)$", "double" },
            
            // Boolean types
            { @"(?i)^(is|has|can|should|will|does|are)", "bool" },
            { @"(?i)(enabled|disabled|visible|valid|active|checked|selected)$", "bool" },
            
            // String types
            { @"(?i)(name|text|message|description|title|path|url|email)$", "string" },
            { @"(?i)(status|state|type|mode|format)$", "string" },
            
            // Collection types
            { @"(?i)(list|items|collection|array)s?$", "IList<object>" },
            { @"(?i)(dict|dictionary|map|lookup)$", "IDictionary<string, object>" },
            { @"s$", "IEnumerable<object>" }, // Plural names often indicate collections
            
            // Date/Time types
            { @"(?i)(date|time|datetime|timestamp|created|updated|modified)$", "DateTime" }
        };

        foreach (var pattern in patterns)
        {
            if (Regex.IsMatch(name, pattern.Key))
            {
                return pattern.Value;
            }
        }

        // Default to object if no pattern matches
        return "object";
    }

    /// <summary>
    /// Helper method to check if a string is a C# keyword
    /// </summary>
    private static bool IsKeyword(string word)
    {
        var keywords = new HashSet<string>
        {
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
            "try", "catch", "finally", "throw", "return", "break", "continue",
            "int", "string", "bool", "double", "float", "decimal", "var", "void",
            "class", "interface", "enum", "struct", "namespace", "using",
            "public", "private", "protected", "internal", "static", "readonly"
        };
        return keywords.Contains(word);
    }

    /// <summary>
    /// Enhanced return requirements data structure
    /// </summary>
    private class EnhancedReturnRequirements
    {
        public bool NeedsReturn { get; set; }
        public string FinalReturnType { get; set; } = "void";
        public string? VariablesToReturn { get; set; }
        public EnhancedVariableAnalysisResult? EnhancedAnalysis { get; set; }
    }
}
