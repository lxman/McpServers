using Esprima;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.TypeScript;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace McpCodeEditor.Services.TypeScript;

/// <summary>
/// Service for TypeScript AST analysis operations
/// Phase A2-T2: Extracted from TypeScriptVariableOperations for focused AST parsing and analysis
/// Simplified implementation focusing on core functionality needed by TypeScriptVariableOperations
/// </summary>
public class TypeScriptAstAnalysisService(ILogger<TypeScriptAstAnalysisService> logger) : ITypeScriptAstAnalysisService
{
    /// <summary>
    /// Validate if a code snippet is a valid TypeScript expression
    /// This is the primary method used by TypeScriptVariableOperations
    /// Phase A2-T2: Extracted from the main TypeScriptVariableOperations class
    /// </summary>
    public bool IsValidTypeScriptExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        try
        {
            logger.LogDebug("Validating TypeScript expression: '{Expression}'", expression);

            // Try to parse as a complete expression by wrapping in parentheses
            var parser = new JavaScriptParser();
            parser.ParseScript($"({expression})");
            
            logger.LogDebug("Expression is valid TypeScript/JavaScript");
            return true;
        }
        catch (ParserException)
        {
            logger.LogDebug("Expression failed AST parsing, trying pattern-based validation");
            
            // Fallback to regex patterns for basic validation
            var patterns = new[]
            {
                @"^\w+$", // Simple identifier
                @"^\w+\.\w+", // Property access
                @"^\w+\(.*\)$", // Function call
                @"^[\w\s\+\-\*\/\(\)\.]+$", // Arithmetic expression
                @"^['`""][^'`""]*['`""]$", // String literal
                @"^\d+(\.\d+)?$", // Number literal
                "^(true|false)$", // Boolean literal
                @"^\[.*\]$", // Array literal
                @"^\{.*\}$" // Object literal
            };

            var isValid = patterns.Any(pattern => Regex.IsMatch(expression.Trim(), pattern));
            logger.LogDebug("Pattern-based validation result: {IsValid}", isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error validating TypeScript expression");
            return false;
        }
    }

    /// <summary>
    /// Infer the type of a TypeScript expression from pattern analysis
    /// Phase A2-T2: Simplified pattern-based implementation
    /// </summary>
    public TypeScriptTypeInference InferExpressionType(string expression, string scopeContext)
    {
        try
        {
            logger.LogDebug("Inferring type for expression: '{Expression}' in scope: {ScopeContext}", expression, scopeContext);

            var inference = InferTypeFromPatterns(expression);
            inference.InferenceMethod = "Pattern";
            logger.LogDebug("Pattern-based type inference: {Type}", inference.InferredType);
            return inference;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Type inference failed for expression: {Expression}", expression);
            return new TypeScriptTypeInference
            {
                Success = false,
                ErrorMessage = $"Type inference failed: {ex.Message}",
                InferenceMethod = "Error"
            };
        }
    }

    #region Private Helper Methods

    private static TypeScriptTypeInference InferTypeFromPatterns(string expression)
    {
        expression = expression.Trim();

        // String literals
        if (Regex.IsMatch(expression, @"^['`""][^'`""]*['`""]$"))
        {
            return new TypeScriptTypeInference
            {
                Success = true,
                InferredType = "string",
                Confidence = 1.0
            };
        }

        // Number literals
        if (Regex.IsMatch(expression, @"^\d+(\.\d+)?$"))
        {
            return new TypeScriptTypeInference
            {
                Success = true,
                InferredType = "number",
                Confidence = 1.0
            };
        }

        // Boolean literals
        if (expression is "true" or "false")
        {
            return new TypeScriptTypeInference
            {
                Success = true,
                InferredType = "boolean",
                Confidence = 1.0
            };
        }

        // Array literals
        if (Regex.IsMatch(expression, @"^\[.*\]$"))
        {
            return new TypeScriptTypeInference
            {
                Success = true,
                InferredType = "any[]",
                Confidence = 0.8
            };
        }

        // Object literals
        if (Regex.IsMatch(expression, @"^\{.*\}$"))
        {
            return new TypeScriptTypeInference
            {
                Success = true,
                InferredType = "object",
                Confidence = 0.8
            };
        }

        // Function calls
        if (Regex.IsMatch(expression, @"\w+\(.*\)"))
        {
            return new TypeScriptTypeInference
            {
                Success = true,
                InferredType = "any",
                Confidence = 0.5
            };
        }

        // Default fallback
        return new TypeScriptTypeInference
        {
            Success = true,
            InferredType = "any",
            Confidence = 0.3
        };
    }

    #endregion
}
