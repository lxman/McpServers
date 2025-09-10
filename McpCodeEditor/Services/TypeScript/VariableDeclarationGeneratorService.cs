using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring.TypeScript;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.TypeScript;

/// <summary>
/// Service for generating TypeScript variable declarations with proper scope-aware syntax
/// Phase A2-T3: Extracted from TypeScriptVariableOperations for better separation of concerns
/// Handles both modern scope-aware declarations and legacy REF-002 compatible methods
/// </summary>
public class VariableDeclarationGeneratorService(ILogger<VariableDeclarationGeneratorService> logger) 
    : IVariableDeclarationGeneratorService
{
    /// <summary>
    /// Generate scope-aware variable declaration with proper TypeScript syntax
    /// Modern approach that uses TypeScriptScopeAnalysisResult for context
    /// </summary>
    public VariableDeclarationResult GenerateScopeAwareVariableDeclaration(
        TypeScriptScopeAnalysisResult scopeAnalysis, 
        string variableName, 
        string selectedExpression, 
        string requestedDeclarationType)
    {
        TypeScriptVariablePlacementStrategy strategy = scopeAnalysis.VariablePlacementStrategy;
        
        logger.LogDebug("TS-013 REF-002: Generating declaration for {PlacementLocation} with {DeclarationType}",
            strategy.PlacementLocation, strategy.DeclarationType);

        switch (strategy.PlacementLocation)
        {
            case VariablePlacementLocation.ClassMember:
                // For class members, use proper TypeScript class member syntax
                string accessModifier = strategy.SuggestedAccessModifier ?? "private";
                var memberDeclaration = $"{accessModifier} readonly {variableName} = {selectedExpression};";
                
                return new VariableDeclarationResult
                {
                    Success = true,
                    Declaration = memberDeclaration,
                    DeclarationType = $"{accessModifier} readonly",
                    PlacementStrategy = strategy,
                    RequiresIndentation = true,
                    SyntaxNote = "Class member syntax - cannot use const/let/var directly in class body"
                };

            case VariablePlacementLocation.MethodLocal:
            case VariablePlacementLocation.FunctionLocal:
            case VariablePlacementLocation.BlockLocal:
                // For method/function/block scope, use standard variable declarations
                string localDeclarationType = IsValidDeclarationType(requestedDeclarationType) ? 
                    requestedDeclarationType : strategy.DeclarationType;
                var localDeclaration = $"{localDeclarationType} {variableName} = {selectedExpression};";
                
                return new VariableDeclarationResult
                {
                    Success = true,
                    Declaration = localDeclaration,
                    DeclarationType = localDeclarationType,
                    PlacementStrategy = strategy,
                    RequiresIndentation = true,
                    SyntaxNote = $"Local variable in {strategy.PlacementLocation} scope"
                };

            case VariablePlacementLocation.ModuleLevel:
                // For module level, use const/let with optional export
                string moduleDeclarationType = IsValidDeclarationType(requestedDeclarationType) ? 
                    requestedDeclarationType : "const";
                var moduleDeclaration = $"{moduleDeclarationType} {variableName} = {selectedExpression};";
                
                return new VariableDeclarationResult
                {
                    Success = true,
                    Declaration = moduleDeclaration,
                    DeclarationType = moduleDeclarationType,
                    PlacementStrategy = strategy,
                    RequiresIndentation = false,
                    SyntaxNote = "Module-level variable"
                };

            default:
                return new VariableDeclarationResult
                {
                    Success = false,
                    ErrorMessage = $"Unsupported placement location: {strategy.PlacementLocation}"
                };
        }
    }

    /// <summary>
    /// REF-002 FIX: Generate variable declaration with proper scope-aware syntax
    /// Legacy method for test compatibility - returns anonymous object matching test expectations
    /// </summary>
    public object GenerateVariableDeclaration(
        string variableName,
        string expression,
        string scopeContext,
        int insertionLine,
        string? typeAnnotation = null)
    {
        logger.LogDebug("REF-002: Generating variable declaration for scope: {ScopeContext}", scopeContext);

        try
        {
            switch (scopeContext.ToLowerInvariant())
            {
                case "class":
                    // REF-002 FIX: For class scope, use proper TypeScript class member syntax
                    string classDeclaration = typeAnnotation != null
                        ? $"private readonly {variableName}: {typeAnnotation} = {expression};"
                        : $"private readonly {variableName} = {expression};";

                    return new
                    {
                        Success = true,
                        Declaration = classDeclaration,
                        DeclarationType = "ClassMember", // REF-002 FIX: Correct declaration type
                        ErrorMessage = (string?)null,
                        SuggestClassProperty = false
                    };

                case "method":
                case "function":
                    // REF-002 FIX: For method/function scope, use const for local variables
                    string localDeclaration = typeAnnotation != null
                        ? $"const {variableName}: {typeAnnotation} = {expression};"
                        : $"const {variableName} = {expression};";

                    return new
                    {
                        Success = true,
                        Declaration = localDeclaration,
                        DeclarationType = "LocalVariable",
                        ErrorMessage = (string?)null,
                        SuggestClassProperty = false
                    };

                case "constructor":
                    // REF-002 FIX: For constructor scope, use const but suggest class property
                    string constructorDeclaration = typeAnnotation != null
                        ? $"const {variableName}: {typeAnnotation} = {expression};"
                        : $"const {variableName} = {expression};";

                    return new
                    {
                        Success = true,
                        Declaration = constructorDeclaration,
                        DeclarationType = "ConstructorVariable",
                        ErrorMessage = (string?)null,
                        SuggestClassProperty = true // REF-002 FIX: Suggest class property for constructor variables
                    };

                default:
                    return new
                    {
                        Success = false,
                        Declaration = string.Empty,
                        DeclarationType = string.Empty,
                        ErrorMessage = $"Unsupported scope context: {scopeContext}",
                        SuggestClassProperty = false
                    };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate variable declaration for scope: {ScopeContext}", scopeContext);
            return new
            {
                Success = false,
                Declaration = string.Empty,
                DeclarationType = string.Empty,
                ErrorMessage = $"Generation failed: {ex.Message}",
                SuggestClassProperty = false
            };
        }
    }

    /// <summary>
    /// REF-002 FIX: Validate variable declaration syntax based on scope context
    /// Legacy method for test compatibility - returns anonymous object matching test expectations
    /// </summary>
    public object ValidateVariableDeclaration(string declaration, string scopeContext)
    {
        logger.LogDebug("REF-002: Validating variable declaration for scope: {ScopeContext}", scopeContext);

        try
        {
            switch (scopeContext.ToLowerInvariant())
            {
                case "class":
                    // REF-002 FIX: Class members cannot use const/let/var directly
                    if (declaration.Contains("const ") || declaration.Contains("let ") || declaration.Contains("var "))
                    {
                        return new
                        {
                            IsValid = false,
                            ErrorMessage = "const/let/var are not valid for class members. Use access modifiers like private, public, or protected instead.",
                            Warnings = new List<string>(),
                            Diagnostics = new List<object>()
                        };
                    }

                    // Should use access modifiers
                    var classWarnings = new List<string>();
                    if (!declaration.Contains("private") && !declaration.Contains("public") && !declaration.Contains("protected"))
                    {
                        classWarnings.Add("Class members should have explicit access modifiers");
                    }

                    return new
                    {
                        IsValid = true,
                        ErrorMessage = string.Empty,
                        Warnings = classWarnings,
                        Diagnostics = new List<object>()
                    };

                case "method":
                case "function":
                    // REF-002 FIX: Method-local variables cannot use access modifiers
                    if (declaration.Contains("private ") || declaration.Contains("public ") || declaration.Contains("protected "))
                    {
                        return new
                        {
                            IsValid = false,
                            ErrorMessage = "private/public/protected access modifiers are not valid for method-local variables. Use const, let, or var instead.",
                            Warnings = new List<string>(),
                            Diagnostics = new List<object>()
                        };
                    }

                    // Should use proper variable declarations
                    var methodWarnings = new List<string>();
                    if (!declaration.Contains("const ") && !declaration.Contains("let ") && !declaration.Contains("var "))
                    {
                        methodWarnings.Add("Method-local variables should use const, let, or var declarations");
                    }

                    return new
                    {
                        IsValid = true,
                        ErrorMessage = string.Empty,
                        Warnings = methodWarnings,
                        Diagnostics = new List<object>()
                    };

                case "constructor":
                    // Similar rules to method scope
                    if (declaration.Contains("private ") || declaration.Contains("public ") || declaration.Contains("protected "))
                    {
                        return new
                        {
                            IsValid = false,
                            ErrorMessage = "Access modifiers are not valid for constructor-local variables",
                            Warnings = new List<string>(),
                            Diagnostics = new List<object>()
                        };
                    }

                    return new
                    {
                        IsValid = true,
                        ErrorMessage = string.Empty,
                        Warnings = new List<string>(),
                        Diagnostics = new List<object>()
                    };

                default:
                    return new
                    {
                        IsValid = false,
                        ErrorMessage = $"Unknown scope context: {scopeContext}",
                        Warnings = new List<string>(),
                        Diagnostics = new List<object>()
                    };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate variable declaration");
            return new
            {
                IsValid = false,
                ErrorMessage = $"Validation failed: {ex.Message}",
                Warnings = new List<string>(),
                Diagnostics = new List<object>()
            };
        }
    }

    /// <summary>
    /// Validate if the requested declaration type is valid for TypeScript
    /// </summary>
    public bool IsValidDeclarationType(string declarationType)
    {
        return declarationType switch
        {
            "const" or "let" or "var" => true,
            _ => false
        };
    }
}
