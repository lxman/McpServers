using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Analysis;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace McpCodeEditor.Services.Validation;

/// <summary>
/// TypeScript-specific validation service for method extraction operations
/// Provides comprehensive validation for TypeScript/JavaScript code extraction
/// </summary>
public class TypeScriptExtractMethodValidator(
    ILogger<TypeScriptExtractMethodValidator> logger,
    TypeScriptAnalysisService analysisService)
{
    // TypeScript/JavaScript language patterns
    private static readonly Regex FunctionCallPattern = new(@"\b\w+\s*\(", RegexOptions.Compiled);
    private static readonly Regex VariableDeclarationPattern = new(@"\b(const|let|var)\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex VariableUsagePattern = new(@"\b(\w+)\s*(?:=|\.|\[|\()", RegexOptions.Compiled);
    private static readonly Regex ReturnStatementPattern = new(@"\breturn\b", RegexOptions.Compiled);
    private static readonly Regex AsyncAwaitPattern = new(@"\b(async|await)\b", RegexOptions.Compiled);
    private static readonly Regex ThisReferencePattern = new(@"\bthis\.", RegexOptions.Compiled);
    private static readonly Regex ControlFlowPattern = new(@"\b(if|else|for|while|switch|try|catch|finally)\b", RegexOptions.Compiled);
    private static readonly Regex TypeScriptKeywords = new(@"\b(abstract|as|asserts|bigint|boolean|break|case|catch|class|const|continue|debugger|declare|default|delete|do|else|enum|export|extends|false|finally|for|from|function|get|if|implements|import|in|infer|instanceof|interface|is|keyof|let|module|namespace|never|new|null|number|object|of|package|private|protected|public|readonly|require|return|set|static|string|super|switch|symbol|this|throw|true|try|type|typeof|undefined|unique|unknown|var|void|while|with|yield)\b", RegexOptions.Compiled);

    /// <summary>
    /// Validate TypeScript code extraction with comprehensive analysis
    /// </summary>
    public async Task<TypeScriptValidationResult> ValidateExtractionAsync(
        string sourceCode,
        TypeScriptExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new TypeScriptValidationResult();
            var analysis = new TypeScriptExtractionAnalysis();

            // Basic validation
            if (!ValidateBasicOptions(options, result))
            {
                return result;
            }

            var lines = sourceCode.Split('\n');

            // Validate line range
            if (!ValidateLineRange(options, lines, result))
            {
                return result;
            }

            // Extract the code to be analyzed
            var extractedLines = new string[options.EndLine - options.StartLine + 1];
            Array.Copy(lines, options.StartLine - 1, extractedLines, 0, extractedLines.Length);
            var extractedCode = string.Join("\n", extractedLines);

            // Perform comprehensive analysis
            await AnalyzeCodeStructureAsync(extractedCode, sourceCode, options, analysis, cancellationToken);
            
            // Validate method name
            ValidateMethodName(options.NewMethodName, result);

            // Validate TypeScript-specific concerns
            ValidateTypeScriptSpecificRules(extractedCode, options, result, analysis);

            // Validate scope and variable usage
            await ValidateVariableScopeAsync(extractedCode, sourceCode, options, analysis, result, cancellationToken);

            // Validate control flow complexity
            ValidateControlFlowComplexity(extractedCode, analysis, result);

            // Generate suggestions
            GenerateExtractionSuggestions(analysis, options, result);

            result.Analysis = analysis;
            result.IsValid = result.Errors.Count == 0;

            logger.LogDebug("TypeScript extraction validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                result.IsValid, result.Errors.Count, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during TypeScript extraction validation");
            return new TypeScriptValidationResult
            {
                IsValid = false,
                Errors = { $"Validation failed: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Validate basic extraction options
    /// </summary>
    private static bool ValidateBasicOptions(TypeScriptExtractionOptions options, TypeScriptValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(options.NewMethodName))
        {
            result.Errors.Add("Method name cannot be empty");
            return false;
        }

        if (options.StartLine < 1 || options.EndLine < 1)
        {
            result.Errors.Add("Line numbers must be positive");
            return false;
        }

        if (options.StartLine > options.EndLine)
        {
            result.Errors.Add("Start line cannot be greater than end line");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate line range against source code
    /// </summary>
    private static bool ValidateLineRange(TypeScriptExtractionOptions options, string[] lines, TypeScriptValidationResult result)
    {
        if (options.EndLine > lines.Length)
        {
            result.Errors.Add($"End line {options.EndLine} exceeds file length ({lines.Length} lines)");
            return false;
        }

        // Check if selected lines contain meaningful code
        var hasNonEmptyLine = false;
        for (var i = options.StartLine - 1; i < options.EndLine; i++)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("//") && !line.StartsWith("/*"))
            {
                hasNonEmptyLine = true;
                break;
            }
        }

        if (!hasNonEmptyLine)
        {
            result.Errors.Add("Selected lines contain no executable code");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Analyze the structure and characteristics of the code to be extracted
    /// </summary>
    private async Task AnalyzeCodeStructureAsync(
        string extractedCode,
        string fullCode,
        TypeScriptExtractionOptions options,
        TypeScriptExtractionAnalysis analysis,
        CancellationToken cancellationToken)
    {
        // Analyze return statements
        analysis.HasReturnStatements = ReturnStatementPattern.IsMatch(extractedCode);

        // Analyze async/await usage
        analysis.HasAsyncAwait = AsyncAwaitPattern.IsMatch(extractedCode);

        // Analyze this references
        analysis.HasThisReferences = ThisReferencePattern.IsMatch(extractedCode);

        // Calculate cyclomatic complexity
        analysis.CyclomaticComplexity = CalculateCyclomaticComplexity(extractedCode);

        // Analyze control flow
        analysis.HasComplexControlFlow = ControlFlowPattern.Matches(extractedCode).Count > 3;

        // Find containing function/class context
        await FindContainingContextAsync(fullCode, options, analysis, cancellationToken);

        // Analyze variable dependencies
        AnalyzeVariableDependencies(extractedCode, fullCode, analysis);
    }

    /// <summary>
    /// Calculate cyclomatic complexity for TypeScript code
    /// </summary>
    private static int CalculateCyclomaticComplexity(string code)
    {
        // Count decision points
        var complexity = 1; // Base complexity

        string[] patterns =
        [
            @"\bif\b", @"\belse\s+if\b", @"\bwhile\b", @"\bfor\b",
            @"\bswitch\b", @"\bcase\b", @"\bcatch\b", @"\b\?\s*.*\s*:\b",
            @"\b&&\b", @"\b\|\|\b"
        ];

        foreach (var pattern in patterns)
        {
            complexity += Regex.Matches(code, pattern, RegexOptions.IgnoreCase).Count;
        }

        return complexity;
    }

    /// <summary>
    /// Find the containing function or class context
    /// </summary>
    private async Task FindContainingContextAsync(
        string fullCode,
        TypeScriptExtractionOptions options,
        TypeScriptExtractionAnalysis analysis,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use the TypeScript analysis service to get structure
            var analysisResult = await analysisService.AnalyzeContentAsync(fullCode, cancellationToken: cancellationToken);

            if (analysisResult.Success)
            {
                // Find containing function
                var containingFunction = analysisResult.Functions.FirstOrDefault(f =>
                    f.StartLine <= options.StartLine && f.EndLine >= options.EndLine);

                if (containingFunction != null)
                {
                    analysis.ContainingFunctionName = containingFunction.Name;
                }

                // Find containing class
                var containingClass = analysisResult.Classes.FirstOrDefault(c =>
                    c.StartLine <= options.StartLine && c.EndLine >= options.EndLine);

                if (containingClass != null)
                {
                    analysis.ContainingClassName = containingClass.Name;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to find containing context");
        }
    }

    /// <summary>
    /// Analyze variable dependencies and scope
    /// </summary>
    private static void AnalyzeVariableDependencies(string extractedCode, string fullCode, TypeScriptExtractionAnalysis analysis)
    {
        // Find variables declared in the extracted code
        var declaredVariables = new HashSet<string>();
        var declarations = VariableDeclarationPattern.Matches(extractedCode);
        foreach (Match match in declarations)
        {
            declaredVariables.Add(match.Groups[2].Value);
        }

        // Find variables used in the extracted code
        var usedVariables = new HashSet<string>();
        var usages = VariableUsagePattern.Matches(extractedCode);
        foreach (Match match in usages)
        {
            var varName = match.Groups[1].Value;
            if (!TypeScriptKeywords.IsMatch(varName) && !declaredVariables.Contains(varName))
            {
                usedVariables.Add(varName);
            }
        }

        analysis.ExternalVariables = usedVariables.ToList();

        // Simple heuristic for modified variables (look for assignments)
        var modifiedVariables = new HashSet<string>();
        string[] assignmentPatterns = [@"(\w+)\s*=", @"(\w+)\s*\+=", @"(\w+)\s*-=", @"(\w+)\+\+", @"(\w+)--"];
        
        foreach (var pattern in assignmentPatterns)
        {
            var assignments = Regex.Matches(extractedCode, pattern);
            foreach (Match match in assignments)
            {
                var varName = match.Groups[1].Value;
                if (usedVariables.Contains(varName))
                {
                    modifiedVariables.Add(varName);
                }
            }
        }

        analysis.ModifiedVariables = modifiedVariables.ToList();
        analysis.HasClosureVariables = analysis.ExternalVariables.Count > 0;
    }

    /// <summary>
    /// Validate TypeScript-specific naming and syntax rules
    /// </summary>
    private static void ValidateMethodName(string methodName, TypeScriptValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            result.Errors.Add("Method name cannot be empty");
            return;
        }

        // TypeScript identifier validation
        if (!IsValidTypeScriptIdentifier(methodName))
        {
            result.Errors.Add($"'{methodName}' is not a valid TypeScript identifier");
        }

        // Naming convention check (camelCase recommended)
        if (!IsCamelCase(methodName))
        {
            result.Warnings.Add($"Method name '{methodName}' should use camelCase convention");
        }

        // Reserved word check
        if (IsTypeScriptReservedWord(methodName))
        {
            result.Errors.Add($"'{methodName}' is a reserved TypeScript keyword");
        }
    }

    /// <summary>
    /// Validate TypeScript-specific extraction rules
    /// </summary>
    private static void ValidateTypeScriptSpecificRules(
        string extractedCode,
        TypeScriptExtractionOptions options,
        TypeScriptValidationResult result,
        TypeScriptExtractionAnalysis analysis)
    {
        // Validate async/await consistency
        if (analysis.HasAsyncAwait && !options.IsAsync)
        {
            result.Warnings.Add("Code contains async/await but method is not marked as async");
        }

        // Validate this references in static context
        if (analysis.HasThisReferences && options.IsStatic)
        {
            result.Errors.Add("Cannot use 'this' references in static methods");
        }

        // Validate function type consistency
        if (options is { FunctionType: TypeScriptFunctionType.ArrowFunction, IsStatic: true })
        {
            result.Warnings.Add("Arrow functions don't support static modifier directly");
        }

        // Check for incomplete statements
        if (!extractedCode.TrimEnd().EndsWith(";") && !extractedCode.TrimEnd().EndsWith("}"))
        {
            result.Warnings.Add("Extracted code may contain incomplete statements");
        }
    }

    /// <summary>
    /// Validate variable scope and closure concerns
    /// </summary>
    private static async Task ValidateVariableScopeAsync(
        string extractedCode,
        string fullCode,
        TypeScriptExtractionOptions options,
        TypeScriptExtractionAnalysis analysis,
        TypeScriptValidationResult result,
        CancellationToken cancellationToken)
    {
        // Check for unresolved variables
        if (analysis.ExternalVariables.Count > 0)
        {
            result.Warnings.Add($"Code references external variables: {string.Join(", ", analysis.ExternalVariables)}");
            result.Warnings.Add("Consider adding parameters for external variables");
        }

        // Check for variable modifications that might affect outer scope
        if (analysis.ModifiedVariables.Count > 0)
        {
            result.Warnings.Add($"Code modifies external variables: {string.Join(", ", analysis.ModifiedVariables)}");
            result.Warnings.Add("Consider returning modified values instead");
        }

        await Task.CompletedTask; // Placeholder for async operations
    }

    /// <summary>
    /// Validate control flow complexity
    /// </summary>
    private static void ValidateControlFlowComplexity(
        string extractedCode,
        TypeScriptExtractionAnalysis analysis,
        TypeScriptValidationResult result)
    {
        if (analysis.CyclomaticComplexity > 10)
        {
            result.Warnings.Add($"High cyclomatic complexity ({analysis.CyclomaticComplexity}). Consider breaking into smaller methods");
        }

        if (analysis.HasComplexControlFlow)
        {
            result.Warnings.Add("Complex control flow detected. Ensure proper error handling in extracted method");
        }
    }

    /// <summary>
    /// Generate helpful suggestions for the extraction
    /// </summary>
    private static void GenerateExtractionSuggestions(
        TypeScriptExtractionAnalysis analysis,
        TypeScriptExtractionOptions options,
        TypeScriptValidationResult result)
    {
        // Suggest parameters for external variables
        if (analysis.ExternalVariables.Count > 0)
        {
            analysis.SuggestedParameters = analysis.ExternalVariables.ToList();
        }

        // Suggest return type based on analysis
        if (analysis.HasReturnStatements)
        {
            analysis.SuggestedReturnType = "any"; // Could be enhanced with type inference
        }
        else if (analysis.ModifiedVariables.Count > 0)
        {
            analysis.SuggestedReturnType = analysis.ModifiedVariables.Count == 1 
                ? "any" // Single modified variable
                : $"{{ {string.Join(", ", analysis.ModifiedVariables.Select(v => $"{v}: any"))} }}"; // Multiple variables as object
        }
        else
        {
            analysis.SuggestedReturnType = "void";
        }

        // Suggest async if code uses await
        if (analysis.HasAsyncAwait && !options.IsAsync)
        {
            result.Warnings.Add("Consider marking method as async due to await usage");
        }
    }

    #region Helper Methods

    /// <summary>
    /// Check if identifier is valid TypeScript identifier
    /// </summary>
    private static bool IsValidTypeScriptIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        // Must start with letter, underscore, or dollar sign
        if (!char.IsLetter(identifier[0]) && identifier[0] != '_' && identifier[0] != '$')
            return false;

        // Rest must be letters, digits, underscores, or dollar signs
        for (var i = 1; i < identifier.Length; i++)
        {
            if (!char.IsLetterOrDigit(identifier[i]) && identifier[i] != '_' && identifier[i] != '$')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Check if string follows camelCase convention
    /// </summary>
    private static bool IsCamelCase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;

        // First character should be lowercase
        if (!char.IsLower(identifier[0]))
            return false;

        // No underscores or spaces
        if (identifier.Contains('_') || identifier.Contains(' '))
            return false;

        return true;
    }

    /// <summary>
    /// Check if string is a TypeScript reserved word
    /// </summary>
    private static bool IsTypeScriptReservedWord(string identifier)
    {
        string[] reservedWords =
        [
            "abstract", "any", "as", "asserts", "bigint", "boolean", "break", "case", "catch",
            "class", "const", "continue", "debugger", "declare", "default", "delete", "do",
            "else", "enum", "export", "extends", "false", "finally", "for", "from", "function",
            "get", "if", "implements", "import", "in", "infer", "instanceof", "interface",
            "is", "keyof", "let", "module", "namespace", "never", "new", "null", "number",
            "object", "of", "package", "private", "protected", "public", "readonly", "require",
            "return", "set", "static", "string", "super", "switch", "symbol", "this", "throw",
            "true", "try", "type", "typeof", "undefined", "unique", "unknown", "var", "void",
            "while", "with", "yield"
        ];

        return reservedWords.Contains(identifier.ToLower());
    }

    #endregion
}
