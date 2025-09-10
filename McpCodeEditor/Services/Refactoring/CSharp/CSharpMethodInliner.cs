using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Models.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace McpCodeEditor.Services.Refactoring.CSharp;

/// <summary>
/// Service for C# method inlining operations.
/// Responsible for replacing method call sites with the method body and removing the method definition.
/// </summary>
public class CSharpMethodInliner : ICSharpMethodInliner
{
    private readonly IPathValidationService _pathValidationService;
    private readonly IBackupService _backupService;
    private readonly IChangeTrackingService _changeTracking;

    /// <summary>
    /// Initializes a new instance of the CSharpMethodInliner service.
    /// </summary>
    /// <param name="pathValidationService">Service for validating and resolving file paths</param>
    /// <param name="backupService">Service for creating backups before modifications</param>
    /// <param name="changeTracking">Service for tracking changes made to files</param>
    public CSharpMethodInliner(
        IPathValidationService pathValidationService,
        IBackupService backupService,
        IChangeTrackingService changeTracking)
    {
        _pathValidationService = pathValidationService;
        _backupService = backupService;
        _changeTracking = changeTracking;
    }

    /// <summary>
    /// Inline a C# method by replacing all call sites with the method body and removing the method definition.
    /// </summary>
    /// <param name="filePath">Path to the C# file containing the method to inline</param>
    /// <param name="options">Method inlining options and configuration</param>
    /// <param name="previewOnly">If true, only preview changes without applying them</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing success status, changes made, and any error messages</returns>
    public async Task<RefactoringResult> InlineMethodAsync(
        string filePath,
        MethodInliningOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RefactoringResult();

            // Validate options first
            MethodInliningValidationResult optionsValidation = options.Validate();
            if (!optionsValidation.IsValid)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Invalid options: {string.Join("; ", optionsValidation.Errors)}"
                };
            }

            // Resolve relative paths to absolute paths
            string resolvedFilePath = _pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = "Failed to parse C# file"
                };
            }

            // Find the method to inline
            MethodDeclarationSyntax? methodToInline = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == options.MethodName);

            if (methodToInline == null)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Method '{options.MethodName}' not found in file"
                };
            }

            // Perform validation for inlining
            MethodInliningValidationResult validationResult = await ValidateMethodForInliningAsync(methodToInline, options);
            if (!validationResult.IsValid)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Method cannot be inlined: {string.Join("; ", validationResult.Errors)}"
                };
            }

            // Extract method body
            if (methodToInline.Body == null)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Method '{options.MethodName}' has no body (expression-bodied or abstract methods not supported)"
                };
            }

            // Get the method body statements (excluding braces)
            string methodBody = string.Join("\n",
                methodToInline.Body.Statements.Select(s => s.ToFullString().Trim()));

            if (string.IsNullOrWhiteSpace(methodBody))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Method '{options.MethodName}' has an empty body"
                };
            }

            // Find all call sites of this method
            List<InvocationExpressionSyntax> invocations = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is IdentifierNameSyntax identifier &&
                             identifier.Identifier.ValueText == options.MethodName)
                .ToList();

            if (invocations.Count == 0)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"No call sites found for method '{options.MethodName}'"
                };
            }

            // Check max call sites limit
            if (options.MaxCallSites > 0 && invocations.Count > options.MaxCallSites)
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"Method has {invocations.Count} call sites, which exceeds the maximum limit of {options.MaxCallSites}"
                };
            }

            // Track changes for each replacement
            CompilationUnitSyntax? modifiedRoot = root;

            // Replace each invocation with the method body
            foreach (InvocationExpressionSyntax invocation in invocations.OrderByDescending(inv => inv.SpanStart))
            {
                modifiedRoot = await ReplaceInvocationWithMethodBodyAsync(
                    modifiedRoot, invocation, methodBody, sourceCode, options, cancellationToken);
            }

            // Remove the original method definition
            modifiedRoot = modifiedRoot.RemoveNode(methodToInline, SyntaxRemoveOptions.KeepNoTrivia);

            // Format the result
            string modifiedContent = await FormatResultAsync(modifiedRoot, options, cancellationToken);

            // Create backup if not preview
            string? backupId = null;
            if (!previewOnly)
            {
                backupId = await _backupService.CreateBackupAsync(
                    Path.GetDirectoryName(resolvedFilePath)!,
                    $"inline_method_{options.MethodName}");
            }

            var change = new FileChange
            {
                FilePath = resolvedFilePath,
                OriginalContent = sourceCode,
                ModifiedContent = modifiedContent,
                ChangeType = "InlineMethod"
            };

            // Apply changes if not preview
            if (!previewOnly)
            {
                await File.WriteAllTextAsync(resolvedFilePath, modifiedContent, cancellationToken);

                await _changeTracking.TrackChangeAsync(
                    resolvedFilePath,
                    sourceCode,
                    modifiedContent,
                    $"Inline method '{options.MethodName}' ({invocations.Count} call sites)",
                    backupId);
            }

            result.Success = true;
            result.Message = previewOnly
                ? $"Preview: Would inline method '{options.MethodName}' at {invocations.Count} call sites"
                : $"Successfully inlined method '{options.MethodName}' at {invocations.Count} call sites";
            result.Changes = [change];
            result.FilesAffected = 1;
            result.Metadata["methodName"] = options.MethodName;
            result.Metadata["callSitesCount"] = invocations.Count.ToString();
            result.Metadata["backupId"] = backupId ?? "";

            // Include validation warnings if any
            if (validationResult.Warnings.Count > 0)
            {
                result.Metadata["warnings"] = string.Join("; ", validationResult.Warnings);
            }

            return result;
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"Method inlining failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validate if a method can be inlined before attempting the operation.
    /// </summary>
    /// <param name="filePath">Path to the C# file containing the method</param>
    /// <param name="methodName">Name of the method to validate for inlining</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Validation result with success status and any validation messages</returns>
    public async Task<MethodInliningValidationResult> ValidateMethodForInliningAsync(
        string filePath,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string resolvedFilePath = _pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return MethodInliningValidationResult.Failure(
                    [new ValidationError("FILE_NOT_FOUND", $"File not found: {filePath}")]);
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                return MethodInliningValidationResult.Failure(
                    [new ValidationError("PARSE_ERROR", "Failed to parse C# file")]);
            }

            MethodDeclarationSyntax? method = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName);

            if (method == null)
            {
                return MethodInliningValidationResult.Failure(
                    [new ValidationError("METHOD_NOT_FOUND", $"Method '{methodName}' not found in file")]);
            }

            return await ValidateMethodForInliningAsync(method, MethodInliningOptions.CreateDefault(methodName));
        }
        catch (Exception ex)
        {
            return MethodInliningValidationResult.Failure(
                [new ValidationError("VALIDATION_FAILED", $"Validation failed: {ex.Message}")]);
        }
    }

    /// <summary>
    /// Get information about a method to help users understand what will be inlined.
    /// </summary>
    /// <param name="filePath">Path to the C# file containing the method</param>
    /// <param name="methodName">Name of the method to analyze</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Method information including call sites, complexity, and recommendations</returns>
    public async Task<MethodInliningInfo> GetMethodInliningInfoAsync(
        string filePath,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        var info = new MethodInliningInfo { MethodName = methodName };

        try
        {
            string resolvedFilePath = _pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                info.InliningRecommendation = $"File not found: {filePath}";
                return info;
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);

            if (await syntaxTree.GetRootAsync(cancellationToken) is not CompilationUnitSyntax root)
            {
                info.InliningRecommendation = "Failed to parse C# file";
                return info;
            }

            MethodDeclarationSyntax? method = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName);

            if (method == null)
            {
                info.InliningRecommendation = $"Method '{methodName}' not found in file";
                return info;
            }

            // Analyze method properties
            info.ReturnType = method.ReturnType.ToString();
            info.ParameterCount = method.ParameterList.Parameters.Count;
            info.IsExpressionBodied = method.ExpressionBody != null;
            info.HasReturnStatements = method.Body?.Statements
                .OfType<ReturnStatementSyntax>().Any() ?? false;
            info.MethodBodyLines = method.Body?.Statements.Count ?? 0;

            // Find call sites
            List<InvocationExpressionSyntax> invocations = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is IdentifierNameSyntax identifier &&
                             identifier.Identifier.ValueText == methodName)
                .ToList();

            info.CallSitesCount = invocations.Count;
            info.CallSiteLocations = invocations.Select(inv =>
                $"Line {syntaxTree.GetLineSpan(inv.Span).StartLinePosition.Line + 1}")
                .ToList();

            // Determine if can be inlined
            MethodInliningValidationResult validation = await ValidateMethodForInliningAsync(method, MethodInliningOptions.CreateDefault(methodName));
            info.CanBeInlined = validation.IsValid;

            // Generate recommendation
            info.InliningRecommendation = GenerateInliningRecommendation(info, validation);

            return info;
        }
        catch (Exception ex)
        {
            info.InliningRecommendation = $"Analysis failed: {ex.Message}";
            return info;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Validate if a method syntax node can be inlined.
    /// </summary>
    private static async Task<MethodInliningValidationResult> ValidateMethodForInliningAsync(
        MethodDeclarationSyntax method,
        MethodInliningOptions options)
    {
        var result = new MethodInliningValidationResult();

        // Basic validation - only support simple methods for now
        if (method.ParameterList.Parameters.Count > 0)
        {
            result.AddError("PARAMETERS_NOT_SUPPORTED", "Inlining methods with parameters is not yet supported");
        }

        if (method.ReturnType.ToString() != "void")
        {
            result.AddError("RETURN_TYPE_NOT_SUPPORTED", "Inlining methods with return values is not yet supported");
        }

        if (method.Body == null)
        {
            result.AddError("EXPRESSION_BODY_NOT_SUPPORTED", "Expression-bodied or abstract methods are not supported");
        }

        // Add warnings for potential issues
        if (method.Body?.Statements.Count > 10)
        {
            result.AddWarning("HIGH_STATEMENT_COUNT", "Method has many statements - inlining may result in significant code duplication");
        }

        List<string> modifiers = method.Modifiers.Select(m => m.ValueText).ToList();
        if (modifiers.Contains("virtual") || modifiers.Contains("abstract") || modifiers.Contains("override"))
        {
            result.AddWarning("POLYMORPHISM_WARNING", "Method uses polymorphism - inlining will remove virtual dispatch behavior");
        }

        return result;
    }

    /// <summary>
    /// Replace a method invocation with the method body.
    /// </summary>
    private static async Task<CompilationUnitSyntax> ReplaceInvocationWithMethodBodyAsync(
        CompilationUnitSyntax root,
        InvocationExpressionSyntax invocation,
        string methodBody,
        string sourceCode,
        MethodInliningOptions options,
        CancellationToken cancellationToken)
    {
        // Find the statement containing this invocation
        var statement = invocation.FirstAncestorOrSelf<StatementSyntax>();
        if (statement == null) return root;

        // Get the indentation of the current statement
        string indentation = GetStatementIndentation(statement, sourceCode);

        // Format the method body with proper indentation
        string[] bodyLines = methodBody.Split('\n');
        string formattedBody = string.Join("\n",
            bodyLines.Select(line => string.IsNullOrWhiteSpace(line) ? line : indentation + line.Trim()));

        // Create replacement statements from the method body
        StatementSyntax[] replacementStatements;
        try
        {
            StatementSyntax parsedStatement = SyntaxFactory.ParseStatement($"{formattedBody}\n");
            replacementStatements = parsedStatement
                .DescendantNodes()
                .OfType<StatementSyntax>()
                .ToArray();

            if (replacementStatements.Length == 0)
            {
                // If parsing failed, create a simple statement
                replacementStatements = [SyntaxFactory.ParseStatement(formattedBody)];
            }
        }
        catch
        {
            // Fallback to simple statement if parsing fails
            replacementStatements = [SyntaxFactory.ParseStatement(formattedBody)];
        }

        // Replace the statement containing the invocation
        var parentBlock = statement.FirstAncestorOrSelf<BlockSyntax>();
        if (parentBlock != null)
        {
            int statementIndex = parentBlock.Statements.IndexOf(statement);
            if (statementIndex >= 0)
            {
                // Remove the old statement and insert the new ones
                SyntaxList<StatementSyntax> newStatements = parentBlock.Statements.RemoveAt(statementIndex);
                for (var i = 0; i < replacementStatements.Length; i++)
                {
                    newStatements = newStatements.Insert(statementIndex + i, replacementStatements[i]);
                }

                BlockSyntax newBlock = parentBlock.WithStatements(newStatements);
                root = root.ReplaceNode(parentBlock, newBlock);
            }
        }

        return root;
    }

    /// <summary>
    /// Format the result using the specified options.
    /// </summary>
    private static async Task<string> FormatResultAsync(
        CompilationUnitSyntax root,
        MethodInliningOptions options,
        CancellationToken cancellationToken)
    {
        if (options.PreserveFormatting)
        {
            var workspace = new AdhocWorkspace();
            SyntaxNode formattedRoot = Formatter.Format(root, workspace, cancellationToken: cancellationToken);
            return formattedRoot.ToFullString();
        }

        return root.ToFullString();
    }

    /// <summary>
    /// Helper method to get the indentation of a statement.
    /// </summary>
    private static string GetStatementIndentation(StatementSyntax statement, string sourceCode)
    {
        string[] lines = sourceCode.Split('\n');
        int statementStart = statement.SpanStart;

        // Find the line containing the statement
        var currentPosition = 0;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (currentPosition <= statementStart && statementStart < currentPosition + lines[lineIndex].Length + 1)
            {
                // Found the line, extract indentation
                return GetLineIndentation(lines[lineIndex]);
            }
            currentPosition += lines[lineIndex].Length + 1; // +1 for newline
        }

        return "";
    }

    /// <summary>
    /// Helper method to get the indentation of a line.
    /// </summary>
    private static string GetLineIndentation(string line)
    {
        var indentEnd = 0;
        while (indentEnd < line.Length && char.IsWhiteSpace(line[indentEnd]))
        {
            indentEnd++;
        }
        return line[..indentEnd];
    }

    /// <summary>
    /// Generate inlining recommendation based on method analysis.
    /// </summary>
    private static string GenerateInliningRecommendation(MethodInliningInfo info, MethodInliningValidationResult validation)
    {
        if (!validation.IsValid)
        {
            return $"Cannot inline: {string.Join(", ", validation.Errors)}";
        }

        if (info.CallSitesCount == 0)
        {
            return "Method has no call sites - consider removing it instead of inlining";
        }

        if (info.CallSitesCount == 1)
        {
            return "Excellent candidate for inlining - single call site with performance benefit";
        }

        if (info is { CallSitesCount: <= 5, MethodBodyLines: <= 3 })
        {
            return "Good candidate for inlining - small method with few call sites";
        }

        if (info.CallSitesCount > 10)
        {
            return "Consider carefully - inlining will create significant code duplication";
        }

        return "Suitable for inlining - review call sites to ensure it's beneficial";
    }

    #endregion
}