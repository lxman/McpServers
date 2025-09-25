using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Services.Validation;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.TypeScript;

namespace McpCodeEditor.Services.Refactoring.TypeScript;

/// <summary>
/// Service for TypeScript variable operations including introduced variable and variable scope analysis
/// Handles TypeScript-specific variable scoping rules (var/let/const) and type inference
/// Enhanced with Esprima AST parsing for reliable expression boundary detection
/// Phase A2 Complete: Now acts as coordinator, delegating to focused services for better maintainability
/// </summary>
public class TypeScriptVariableOperations(
    ILogger<TypeScriptVariableOperations> logger,
    IPathValidationService pathValidationService,
    TypeScriptExtractMethodValidator validator,
    TypeScriptScopeAnalyzer scopeAnalyzer,
    ITypeScriptSyntaxValidator syntaxValidator,
    IExpressionBoundaryDetectionService boundaryDetectionService,
    IVariableDeclarationGeneratorService variableDeclarationGeneratorService,
    ITypeScriptAstAnalysisService astAnalysisService,
    ITypeScriptCodeModificationService codeModificationService) // Phase A2-T6: Added code modification service
    : ITypeScriptVariableOperations
{
    private readonly TypeScriptExtractMethodValidator _validator = validator;
    private readonly IExpressionBoundaryDetectionService _boundaryDetectionService = boundaryDetectionService;
    private readonly IVariableDeclarationGeneratorService _variableDeclarationGeneratorService = variableDeclarationGeneratorService;
    private readonly ITypeScriptAstAnalysisService _astAnalysisService = astAnalysisService;
    private readonly ITypeScriptCodeModificationService _codeModificationService = codeModificationService; // Phase A2-T6: Added field

    #region REF-002 FIX: Private Methods Expected by Tests - Now delegated to service

    /// <summary>
    /// REF-002 FIX: Generate variable declaration with proper scope-aware syntax
    /// Phase A2-T3: Delegates to IVariableDeclarationGeneratorService
    /// This method signature matches what the tests expect via reflection
    /// Returns exactly the same type structure as test's VariableDeclarationResult
    /// </summary>
    private object GenerateVariableDeclaration(
        string variableName,
        string expression,
        string scopeContext,
        int insertionLine,
        string? typeAnnotation = null)
    {
        // Phase A2-T3: Delegate to extracted service
        return _variableDeclarationGeneratorService.GenerateVariableDeclaration(
            variableName, expression, scopeContext, insertionLine, typeAnnotation);
    }

    /// <summary>
    /// REF-002 FIX: Validate variable declaration syntax based on scope context
    /// Phase A2-T3: Delegates to IVariableDeclarationGeneratorService
    /// This method signature matches what the tests expect via reflection
    /// Returns exactly the same type structure as test's ValidationResult
    /// </summary>
    private object ValidateVariableDeclaration(string declaration, string scopeContext)
    {
        // Phase A2-T3: Delegate to extracted service
        return _variableDeclarationGeneratorService.ValidateVariableDeclaration(declaration, scopeContext);
    }

    #endregion

    #region REF-002 FIX: Scope Detection (unchanged)

    /// <summary>
    /// REF-002 FIX: Detect scope context from file content and line number
    /// This method signature matches what the tests expect via reflection
    /// Returns exactly the same type structure as test's ScopeDetectionResult
    /// </summary>
    private object DetectScope(string fileContent, int lineNumber)
    {
        logger.LogDebug("REF-002: Detecting scope at line: {LineNumber}", lineNumber);

        try
        {
            var lines = fileContent.Split('\n');
            
            if (lineNumber < 1 || lineNumber > lines.Length)
            {
                return new
                {
                    Success = false,
                    ScopeType = string.Empty,
                    ClassName = (string?)null,
                    MethodName = (string?)null,
                    ErrorMessage = $"Line number {lineNumber} is out of range"
                };
            }

            // Use the scope analyzer to get detailed scope information
            var analysis = scopeAnalyzer.AnalyzeScope(lines, lineNumber);
            
            if (!analysis.Success)
            {
                return new
                {
                    Success = false,
                    ScopeType = string.Empty,
                    ClassName = (string?)null,
                    MethodName = (string?)null,
                    ErrorMessage = analysis.ErrorMessage
                };
            }

            // Convert to the format expected by tests
            var scopeType = ConvertScopeTypeForTests(analysis.PrimaryScopeType);
            var className = GetClassNameFromScopes(analysis.ScopeHierarchy);
            var methodName = GetMethodNameFromScopes(analysis.ScopeHierarchy);

            return new
            {
                Success = true,
                ScopeType = scopeType,
                ClassName = className,
                MethodName = methodName,
                ErrorMessage = (string?)null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to detect scope at line: {LineNumber}", lineNumber);
            return new
            {
                Success = false,
                ScopeType = string.Empty,
                ClassName = (string?)null,
                MethodName = (string?)null,
                ErrorMessage = $"Scope detection failed: {ex.Message}"
            };
        }
    }

    #endregion

    #region REF-002 FIX: Full introduce variable operation expected by tests

    /// <summary>
    /// REF-002 FIX: Full introduce variable operation expected by tests
    /// This method signature matches what the tests expect via reflection
    /// Returns exactly the same type structure as test's IntroduceVariableResult
    /// Phase A2 Complete: Now coordinates between multiple focused services
    /// </summary>
    private object IntroduceVariable(
        string fileContent,
        int line,
        int startColumn,
        int endColumn,
        string variableName)
    {
        logger.LogDebug("REF-002: Introducing variable: {VariableName} at line {Line}", variableName, line);

        try
        {
            var lines = fileContent.Split('\n');
            
            // Detect scope first
            var scopeResult = DetectScope(fileContent, line);
            var scopeResultType = scopeResult.GetType();
            var scopeSuccess = (bool)scopeResultType.GetProperty("Success")!.GetValue(scopeResult)!;
            
            if (!scopeSuccess)
            {
                var scopeErrorMessage = (string)scopeResultType.GetProperty("ErrorMessage")!.GetValue(scopeResult)!;
                return new
                {
                    Success = false,
                    ModifiedCode = string.Empty,
                    ErrorMessage = scopeErrorMessage
                };
            }

            var scopeType = (string)scopeResultType.GetProperty("ScopeType")!.GetValue(scopeResult)!;

            // Extract expression
            if (line < 1 || line > lines.Length)
            {
                return new
                {
                    Success = false,
                    ModifiedCode = string.Empty,
                    ErrorMessage = $"Line number {line} is out of range"
                };
            }

            var lineContent = lines[line - 1];
            if (startColumn < 1 || endColumn > lineContent.Length || startColumn > endColumn)
            {
                return new
                {
                    Success = false,
                    ModifiedCode = string.Empty,
                    ErrorMessage = $"Invalid column range: {startColumn}-{endColumn}"
                };
            }

            // Phase A2: Delegate boundary detection to injected service
            var boundaryResult = _boundaryDetectionService.DetectExpressionBoundaries(lineContent, startColumn, endColumn);
            if (!boundaryResult.Success)
            {
                return new
                {
                    Success = false,
                    ModifiedCode = string.Empty,
                    ErrorMessage = $"Expression boundary detection failed: {boundaryResult.ErrorMessage}"
                };
            }

            var expression = boundaryResult.Expression;
            var adjustedStart = boundaryResult.StartColumn;
            var adjustedEnd = boundaryResult.EndColumn;

            // Phase A2: Generate variable declaration using extracted service
            var declResult = GenerateVariableDeclaration(variableName, expression, scopeType, line);
            var declResultType = declResult.GetType();
            var declSuccess = (bool)declResultType.GetProperty("Success")!.GetValue(declResult)!;

            if (!declSuccess)
            {
                var declErrorMessage = (string)declResultType.GetProperty("ErrorMessage")!.GetValue(declResult)!;
                return new
                {
                    Success = false,
                    ModifiedCode = string.Empty,
                    ErrorMessage = declErrorMessage
                };
            }

            var declaration = (string)declResultType.GetProperty("Declaration")!.GetValue(declResult)!;

            // Create modified code
            var modifiedLines = new List<string>(lines);
            
            // Insert variable declaration at appropriate location
            var insertionLine = FindInsertionPoint(lines, line, scopeType);
            var indentation = GetProperIndentation(lines, insertionLine, scopeType);
            modifiedLines.Insert(insertionLine, indentation + declaration);

            // Replace original expression with variable reference
            var originalLine = lines[line - 1];
            var variableRef = scopeType == "class" ? $"this.{variableName}" : variableName;
            
            // Adjust line index if insertion happened before target line
            var targetLineIndex = insertionLine <= line - 1 ? line : line - 1;
            
            var replacedLine = originalLine[..(adjustedStart - 1)] + 
                               variableRef + 
                               originalLine[adjustedEnd..];
            
            modifiedLines[targetLineIndex] = replacedLine;

            return new
            {
                Success = true,
                ModifiedCode = string.Join(Environment.NewLine, modifiedLines),
                ErrorMessage = (string?)null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to introduce variable: {VariableName}", variableName);
            return new
            {
                Success = false,
                ModifiedCode = string.Empty,
                ErrorMessage = $"Variable introduction failed: {ex.Message}"
            };
        }
    }

    #endregion

    #region Helper Methods for REF-002 Fix

    private static string ConvertScopeTypeForTests(TypeScriptScopeType scopeType)
    {
        return scopeType switch
        {
            TypeScriptScopeType.Class => "class",
            TypeScriptScopeType.Method => "method",
            TypeScriptScopeType.Constructor => "constructor",
            TypeScriptScopeType.Function => "function",
            _ => "method"
        };
    }

    private static string? GetClassNameFromScopes(List<TypeScriptScopeInfo> scopes)
    {
        return scopes.FirstOrDefault(s => s.ScopeType == TypeScriptScopeType.Class)?.Name;
    }

    private static string? GetMethodNameFromScopes(List<TypeScriptScopeInfo> scopes)
    {
        var methodScope = scopes.LastOrDefault(s => 
            s.ScopeType is TypeScriptScopeType.Method or TypeScriptScopeType.Constructor or TypeScriptScopeType.Function);
        
        if (methodScope?.ScopeType == TypeScriptScopeType.Constructor)
            return "constructor";
        
        return methodScope?.Name;
    }

    private static int FindInsertionPoint(string[] lines, int currentLine, string scopeType)
    {
        switch (scopeType.ToLowerInvariant())
        {
            case "class":
                // Find a good spot in the class - after existing properties but before methods
                for (var i = Math.Max(0, currentLine - 10); i < Math.Min(currentLine - 1, lines.Length); i++)
                {
                    var line = lines[i].Trim();
                    if (line.Contains("constructor") || (line.Contains("(") && line.Contains("{")))
                    {
                        return i;
                    }
                }
                return Math.Max(0, currentLine - 1);

            case "method":
            case "constructor":
            case "function":
                // Insert at beginning of method
                for (var i = Math.Max(0, currentLine - 5); i < currentLine - 1; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Contains("{") && !line.Contains("="))
                    {
                        return i + 1;
                    }
                }
                return Math.Max(0, currentLine - 1);

            default:
                return Math.Max(0, currentLine - 1);
        }
    }

    private static string GetProperIndentation(string[] lines, int lineIndex, string scopeType)
    {
        // Look at nearby lines to determine appropriate indentation
        for (var i = Math.Max(0, lineIndex - 2); i <= Math.Min(lineIndex + 2, lines.Length - 1); i++)
        {
            if (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                var indentation = GetIndentation(lines[i]);
                // For class members, add extra indentation
                if (scopeType == "class")
                {
                    return indentation.Length > 0 ? indentation : "    ";
                }
                return indentation;
            }
        }

        return scopeType == "class" ? "    " : "";
    }

    #endregion

    /// <summary>
    /// Introduce a TypeScript variable from the selected expression with smart boundary detection
    /// REF-002 FIXED: Now uses scope-aware variable placement with proper TypeScript syntax
    /// Phase A2 Complete: Acts as coordinator delegating to focused services
    /// </summary>
    public async Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName = null,
        string declarationType = "const",
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting TypeScript introduce variable: {FilePath}, line {Line}, columns {StartColumn}-{EndColumn}",
                filePath, line, startColumn, endColumn);

            // Validate file path
            string resolvedPath;
            try
            {
                resolvedPath = pathValidationService.ValidateAndResolvePath(filePath);
            }
            catch (Exception ex)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }

            // Validate TypeScript file
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!IsTypeScriptFile(extension))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Read file content
            var lines = await File.ReadAllLinesAsync(resolvedPath, cancellationToken);

            if (line < 1 || line > lines.Length)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Line number {line} is out of range (file has {lines.Length} lines)"
                };
            }

            // REF-002 FIX: Analyze scope context BEFORE processing expression
            var scopeAnalysis = scopeAnalyzer.AnalyzeScope(lines, line);
            if (!scopeAnalysis.Success)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Scope analysis failed: {scopeAnalysis.ErrorMessage}"
                };
            }

            logger.LogInformation("TS-013 REF-002: Detected scope type: {ScopeType}, Strategy: {Strategy}",
                scopeAnalysis.PrimaryScopeType, scopeAnalysis.VariablePlacementStrategy.DeclarationType);

            // Extract the selected expression with smart boundary detection
            var lineContent = lines[line - 1];
            if (startColumn < 1 || endColumn > lineContent.Length || startColumn > endColumn)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid column range: {startColumn}-{endColumn} for line with {lineContent.Length} characters"
                };
            }

            // Phase A2: Delegate boundary detection to injected service
            var boundaryResult = _boundaryDetectionService.DetectExpressionBoundaries(lineContent, startColumn, endColumn);
            if (!boundaryResult.Success)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = boundaryResult.ErrorMessage
                };
            }

            var selectedExpression = boundaryResult.Expression;
            var adjustedStartColumn = boundaryResult.StartColumn;
            var adjustedEndColumn = boundaryResult.EndColumn;

            logger.LogInformation("TS-013 REF-002: Boundary detection - Original: {OriginalStart}-{OriginalEnd}, " +
                                 "Adjusted: {AdjustedStart}-{AdjustedEnd} '{AdjustedExpr}'",
                startColumn, endColumn, adjustedStartColumn, adjustedEndColumn, selectedExpression);

            if (string.IsNullOrWhiteSpace(selectedExpression))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Selected text is empty or whitespace"
                };
            }

            // Phase A2: Delegate expression validation to AST analysis service
            if (!_astAnalysisService.IsValidTypeScriptExpression(selectedExpression))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Selected text does not appear to be a valid TypeScript expression"
                };
            }

            // Generate a variable name if not provided
            if (string.IsNullOrWhiteSpace(variableName))
            {
                variableName = GenerateTypeScriptVariableName(selectedExpression);
            }

            // Validate variable name
            if (!IsValidTypeScriptIdentifier(variableName))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid TypeScript variable name: '{variableName}'"
                };
            }

            // Phase A2: Use extracted service for variable declaration generation
            var variableDeclarationResult = _variableDeclarationGeneratorService.GenerateScopeAwareVariableDeclaration(
                scopeAnalysis, variableName, selectedExpression, declarationType);

            if (!variableDeclarationResult.Success)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = variableDeclarationResult.ErrorMessage
                };
            }
            
            var syntaxValidationResult = syntaxValidator.ValidateVariableDeclaration(
                variableDeclarationResult.Declaration, scopeAnalysis.PrimaryScopeType);
            
            if (!syntaxValidationResult.IsValid)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Generated variable declaration contains syntax errors: {syntaxValidationResult.Message}"
                };
            }

            // Phase A2-T6: Delegate code modification to extracted service
            var modificationResult = _codeModificationService.ApplyScopeAwareVariableIntroduction(
                lines, line, adjustedStartColumn, adjustedEndColumn, 
                variableName, selectedExpression, variableDeclarationResult, 
                scopeAnalysis);

            if (!modificationResult.Success)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = modificationResult.ErrorMessage
                };
            }

            // Create FileChange for change tracking
            var changes = new List<FileChange>
            {
                new FileChange
                {
                    FilePath = filePath,
                    OriginalContent = string.Join(Environment.NewLine, lines),
                    ModifiedContent = string.Join(Environment.NewLine, modificationResult.ModifiedLines),
                    ChangeType = "IntroduceTypeScriptVariable"
                }
            };

            if (previewOnly)
            {
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would introduce TypeScript variable '{variableName}' " +
                             $"using {scopeAnalysis.VariablePlacementStrategy.DeclarationType} declaration " +
                             $"in {scopeAnalysis.PrimaryScopeType} scope " +
                             $"(boundaries adjusted from {startColumn}-{endColumn} to {adjustedStartColumn}-{adjustedEndColumn})",
                    Changes = changes
                };
            }

            // Write modified content
            await File.WriteAllLinesAsync(resolvedPath, modificationResult.ModifiedLines, cancellationToken);

            return new RefactoringResult
            {
                Success = true,
                Message = $"Successfully introduced TypeScript variable '{variableName}' " +
                         $"using {scopeAnalysis.VariablePlacementStrategy.DeclarationType} declaration " +
                         $"in {scopeAnalysis.PrimaryScopeType} scope " +
                         $"(boundaries adjusted from {startColumn}-{endColumn} to {adjustedStartColumn}-{adjustedEndColumn})",
                FilesAffected = 1,
                Changes = changes
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TypeScript introduce variable failed for {FilePath}", filePath);
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript introduce variable failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Analyze TypeScript variable scope and usage patterns
    /// </summary>
    public async Task<TypeScriptVariableScopeAnalysis> AnalyzeVariableScopeAsync(
        string filePath,
        string variableName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string resolvedPath;
            try
            {
                resolvedPath = pathValidationService.ValidateAndResolvePath(filePath);
            }
            catch (Exception ex)
            {
                return new TypeScriptVariableScopeAnalysis
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
            }

            var content = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
            var lines = content.Split('\n');

            var analysis = new TypeScriptVariableScopeAnalysis
            {
                IsValid = true,
                VariableName = variableName,
                FilePath = filePath
            };

            // Find variable declarations
            for (var i = 0; i < lines.Length; i++)
            {
                var pattern = $@"\b(var|let|const)\s+{Regex.Escape(variableName)}\b";
                var declMatches = Regex.Matches(lines[i], pattern);
                foreach (Match match in declMatches)
                {
                    analysis.Declarations.Add(new TypeScriptVariableDeclaration
                    {
                        LineNumber = i + 1,
                        Column = match.Index + 1,
                        DeclarationType = match.Groups[1].Value,
                        ScopeType = DetermineScopeType(lines, i, match.Groups[1].Value)
                    });
                }
            }

            // Find variable usages
            for (var i = 0; i < lines.Length; i++)
            {
                var pattern = $@"\b{Regex.Escape(variableName)}\b";
                var usageMatches = Regex.Matches(lines[i], pattern);
                foreach (Match match in usageMatches)
                {
                    // Skip if this is a declaration
                    if (analysis.Declarations.Any(d => d.LineNumber == i + 1 && Math.Abs(d.Column - match.Index - 1) < 10))
                        continue;

                    analysis.Usages.Add(new TypeScriptVariableUsage
                    {
                        LineNumber = i + 1,
                        Column = match.Index + 1,
                        UsageType = DetermineUsageType(lines[i], match.Index)
                    });
                }
            }

            return analysis;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TypeScript variable scope analysis failed for {FilePath}", filePath);
            return new TypeScriptVariableScopeAnalysis
            {
                IsValid = false,
                ErrorMessage = $"Analysis failed: {ex.Message}"
            };
        }
    }

    #region Private Helper Methods

    private static bool IsTypeScriptFile(string extension)
    {
        return extension switch
        {
            ".ts" or ".tsx" or ".js" or ".jsx" => true,
            _ => false
        };
    }

    private static string GenerateTypeScriptVariableName(string expression)
    {
        // Clean the expression and create a meaningful variable name
        var cleanExpression = Regex.Replace(expression, @"[^\w]", "");
        
        if (string.IsNullOrWhiteSpace(cleanExpression))
            return "value";

        // Convert to camelCase
        var variableName = cleanExpression.Length == 1 
            ? cleanExpression.ToLowerInvariant()
            : char.ToLowerInvariant(cleanExpression[0]) + cleanExpression[1..];

        // Add context-based suffixes
        if (expression.Contains("()"))
            return variableName + "Result";
        if (expression.Contains(".length"))
            return variableName.Replace("length", "Count");
        if (expression.Contains("+") || expression.Contains("-") || expression.Contains("*") || expression.Contains("/"))
            return "calculation";

        return variableName.Length > 0 ? variableName : "value";
    }

    private static bool IsValidTypeScriptIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must start with letter, underscore, or dollar sign
        if (!Regex.IsMatch(name, @"^[a-zA-Z_$][a-zA-Z0-9_$]*$"))
            return false;

        // Check against TypeScript/JavaScript reserved words
        var reservedWords = new HashSet<string>
        {
            "break", "case", "catch", "class", "const", "continue", "debugger", "default",
            "delete", "do", "else", "enum", "export", "extends", "false", "finally",
            "for", "function", "if", "import", "in", "instanceof", "new", "null",
            "return", "super", "switch", "this", "throw", "true", "try", "typeof",
            "var", "void", "while", "with", "yield", "let", "static", "implements",
            "interface", "package", "private", "protected", "public", "any", "boolean",
            "number", "string", "symbol", "type", "from", "of", "as", "namespace"
        };

        return !reservedWords.Contains(name.ToLowerInvariant());
    }

    private static string GetIndentation(string line)
    {
        var indentCount = 0;
        foreach (var c in line)
        {
            if (c == ' ')
                indentCount++;
            else if (c == '\t')
                indentCount += 4; // Assume 4 spaces per tab
            else
                break;
        }
        return new string(' ', indentCount);
    }

    private static TypeScriptScopeType DetermineScopeType(string[] lines, int lineIndex, string declarationType)
    {
        // Simplified scope determination
        return declarationType switch
        {
            "var" => TypeScriptScopeType.Function,
            "let" or "const" => TypeScriptScopeType.Block,
            _ => TypeScriptScopeType.Module
        };
    }

    private static TypeScriptVariableUsageType DetermineUsageType(string line, int position)
    {
        // Check if it's an assignment
        if (line[position..].Contains("=") && 
            !line[..position].Contains("="))
        {
            return TypeScriptVariableUsageType.Assignment;
        }

        return TypeScriptVariableUsageType.Read;
    }

    #endregion
}
