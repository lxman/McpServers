using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring.TypeScript;

namespace McpCodeEditor.Services.Refactoring;

/// <summary>
/// Unified TypeScript refactoring service that coordinates all TypeScript refactoring operations
/// Now uses the enhanced AST-based TypeScript method extraction for accurate, context-preserving refactoring
/// </summary>
public class TypeScriptRefactoringService(
    TypeScriptMethodExtractor methodExtractor,  // Using the AST-based extractor
    ITypeScriptVariableOperations variableOperations,
    ITypeScriptMethodInliner methodInliner,
    ITypeScriptImportManager importManager,
    IPathValidationService pathValidationService)
    : ITypeScriptRefactoringService
{
    /// <summary>
    /// Extract a TypeScript method from selected lines with comprehensive validation
    /// Now uses AST-based extraction with proper context preservation
    /// </summary>
    public async Task<TypeScriptMethodExtractor.TypeScriptExtractionResult> ExtractMethodAsync(
        string filePath,
        TypeScriptExtractionOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve relative paths to absolute paths
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new TypeScriptMethodExtractor.TypeScriptExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}"
                };
            }

            // Validate file is TypeScript/JavaScript
            string extension = Path.GetExtension(resolvedFilePath).ToLowerInvariant();
            if (extension != ".ts" && extension != ".tsx" && extension != ".js" && extension != ".jsx")
            {
                return new TypeScriptMethodExtractor.TypeScriptExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            string sourceCode = await File.ReadAllTextAsync(resolvedFilePath, cancellationToken);

            // Delegate to the enhanced AST-based method extractor
            return await methodExtractor.ExtractMethodAsync(
                resolvedFilePath,
                sourceCode,
                options,
                !previewOnly, // Create backup if not preview
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new TypeScriptMethodExtractor.TypeScriptExtractionResult
            {
                Success = false,
                ErrorMessage = $"TypeScript method extraction failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Introduce a TypeScript variable from selected expression
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
            // Resolve relative paths to absolute paths
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            // Validate file is TypeScript/JavaScript
            string extension = Path.GetExtension(resolvedFilePath).ToLowerInvariant();
            if (extension != ".ts" && extension != ".tsx" && extension != ".js" && extension != ".jsx")
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Delegate to the specialized variable operations service
            return await variableOperations.IntroduceVariableAsync(
                resolvedFilePath,
                line,
                startColumn,
                endColumn,
                variableName,
                declarationType,
                previewOnly,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript variable introduction failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Inline a TypeScript function by replacing all call sites with function body
    /// </summary>
    public async Task<RefactoringResult> InlineFunctionAsync(
        string filePath,
        string functionName,
        string inlineScope = "file",
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve relative paths to absolute paths
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            // Validate file is TypeScript/JavaScript
            string extension = Path.GetExtension(resolvedFilePath).ToLowerInvariant();
            if (extension != ".ts" && extension != ".tsx" && extension != ".js" && extension != ".jsx")
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Delegate to the specialized method inliner service
            return await methodInliner.InlineFunctionAsync(
                resolvedFilePath,
                functionName,
                inlineScope,
                previewOnly,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript function inlining failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Organize and sort TypeScript import statements
    /// </summary>
    public async Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool sortAlphabetically = true,
        bool groupByType = true,
        bool removeUnused = false,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve relative paths to absolute paths
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            // Validate file is TypeScript/JavaScript
            string extension = Path.GetExtension(resolvedFilePath).ToLowerInvariant();
            if (extension != ".ts" && extension != ".tsx" && extension != ".js" && extension != ".jsx")
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Delegate to the specialized import manager service
            return await importManager.OrganizeImportsAsync(
                resolvedFilePath,
                sortAlphabetically,
                groupByType,
                removeUnused,
                previewOnly,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript import organization failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Add an import statement to a TypeScript file
    /// </summary>
    public async Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve relative paths to absolute paths
            string resolvedFilePath = pathValidationService.ValidateAndResolvePath(filePath);

            if (!File.Exists(resolvedFilePath))
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File not found: {filePath}"
                };
            }

            // Validate file is TypeScript/JavaScript
            string extension = Path.GetExtension(resolvedFilePath).ToLowerInvariant();
            if (extension != ".ts" && extension != ".tsx" && extension != ".js" && extension != ".jsx")
            {
                return new RefactoringResult
                {
                    Success = false,
                    Error = $"File is not a TypeScript/JavaScript file: {extension}"
                };
            }

            // Delegate to the specialized import manager service
            return await importManager.AddImportAsync(
                resolvedFilePath,
                importStatement,
                previewOnly,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new RefactoringResult
            {
                Success = false,
                Error = $"TypeScript import addition failed: {ex.Message}"
            };
        }
    }
}
