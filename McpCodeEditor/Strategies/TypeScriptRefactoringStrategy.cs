using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring;
using McpCodeEditor.Services.Refactoring.TypeScript;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Strategies;

/// <summary>
/// TypeScript/JavaScript language-specific refactoring strategy implementation.
/// Handles all TypeScript and JavaScript refactoring operations using dedicated TypeScript services.
/// </summary>
public class TypeScriptRefactoringStrategy : ILanguageRefactoringStrategy
{
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly TypeScriptSymbolRenameService _symbolRenameService;
    private readonly ILogger<TypeScriptRefactoringStrategy> _logger;

    public LanguageType Language => LanguageType.TypeScript; // Also handles JavaScript

    public TypeScriptRefactoringStrategy(
        ITypeScriptRefactoringService typeScriptRefactoringService,
        TypeScriptSymbolRenameService symbolRenameService,
        ILogger<TypeScriptRefactoringStrategy> logger)
    {
        _typeScriptRefactoringService = typeScriptRefactoringService ?? throw new ArgumentNullException(nameof(typeScriptRefactoringService));
        _symbolRenameService = symbolRenameService ?? throw new ArgumentNullException(nameof(symbolRenameService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RefactoringResult> ExtractMethodAsync(
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TypeScript ExtractMethodAsync (basic) called: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}",
            filePath, methodName, startLine, endLine, previewOnly);

        try
        {
            var options = new TypeScriptExtractionOptions
            {
                NewMethodName = methodName,
                StartLine = startLine,
                EndLine = endLine,
                FunctionType = TypeScriptFunctionType.Function,
                ExportMethod = false
            };

            var result = await _typeScriptRefactoringService.ExtractMethodAsync(filePath, options, previewOnly, cancellationToken);
            return ConvertToRefactoringResult(result, methodName, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeScript method extraction (basic)");
            return CreateErrorResult($"TypeScript method extraction failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> ExtractMethodAsync(
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly,
        string accessModifier,
        bool isStatic,
        string? returnType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TypeScript ExtractMethodAsync (advanced) called: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}, accessModifier={AccessModifier}, isStatic={IsStatic}, returnType={ReturnType}",
            filePath, methodName, startLine, endLine, previewOnly, accessModifier, isStatic, returnType);

        try
        {
            var options = new TypeScriptExtractionOptions
            {
                NewMethodName = methodName,
                StartLine = startLine,
                EndLine = endLine,
                AccessModifier = accessModifier,
                IsStatic = isStatic,
                ReturnType = returnType,
                FunctionType = TypeScriptFunctionType.Function,
                ExportMethod = accessModifier?.ToLower() == "export"
            };

            var result = await _typeScriptRefactoringService.ExtractMethodAsync(filePath, options, previewOnly, cancellationToken);
            return ConvertToRefactoringResult(result, methodName, filePath, accessModifier, isStatic, returnType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeScript method extraction (advanced)");
            return CreateErrorResult($"TypeScript method extraction failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> InlineMethodAsync(
        string filePath,
        string methodName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TypeScript InlineMethodAsync called: filePath={FilePath}, methodName={MethodName}, previewOnly={PreviewOnly}",
            filePath, methodName, previewOnly);

        try
        {
            return await _typeScriptRefactoringService.InlineFunctionAsync(filePath, methodName, "file", previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeScript method inlining");
            return CreateErrorResult($"TypeScript method inlining failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TypeScript IntroduceVariableAsync called: filePath={FilePath}, line={Line}, startColumn={StartColumn}, endColumn={EndColumn}, variableName={VariableName}, previewOnly={PreviewOnly}",
            filePath, line, startColumn, endColumn, variableName, previewOnly);

        try
        {
            return await _typeScriptRefactoringService.IntroduceVariableAsync(filePath, line, startColumn, endColumn, variableName, "const", previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeScript variable introduction");
            return CreateErrorResult($"TypeScript variable introduction failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool removeUnused = true,
        bool sortAlphabetically = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TypeScript OrganizeImportsAsync called: filePath={FilePath}, removeUnused={RemoveUnused}, sortAlphabetically={SortAlphabetically}, previewOnly={PreviewOnly}",
            filePath, removeUnused, sortAlphabetically, previewOnly);

        try
        {
            return await _typeScriptRefactoringService.OrganizeImportsAsync(filePath, sortAlphabetically, true, removeUnused, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeScript import organization");
            return CreateErrorResult($"TypeScript import organization failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TypeScript AddImportAsync called: filePath={FilePath}, importStatement={ImportStatement}, previewOnly={PreviewOnly}",
            filePath, importStatement, previewOnly);

        try
        {
            return await _typeScriptRefactoringService.AddImportAsync(filePath, importStatement, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeScript import addition");
            return CreateErrorResult($"TypeScript import addition failed: {ex.Message}");
        }
    }

    public Task<RefactoringResult> EncapsulateFieldAsync(
        string filePath,
        string fieldName,
        string? propertyName = null,
        bool useAutoProperty = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("TypeScript EncapsulateFieldAsync called - operation not supported for TypeScript");
        return Task.FromResult(CreateNotSupportedResult("Field encapsulation is not supported for TypeScript/JavaScript files"));
    }

    public async Task<RefactoringResult> RenameSymbolAsync(
        string? filePath,
        string symbolName,
        string newName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TypeScript RenameSymbolAsync called: filePath={FilePath}, symbolName={SymbolName}, newName={NewName}, previewOnly={PreviewOnly}",
            filePath, symbolName, newName, previewOnly);

        try
        {
            return await _symbolRenameService.RenameSymbolAsync(symbolName, newName, filePath ?? "", previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TypeScript symbol rename");
            return CreateErrorResult($"TypeScript symbol rename failed: {ex.Message}");
        }
    }

    public bool IsOperationSupported(string operationType)
    {
        return operationType.ToLowerInvariant() switch
        {
            "extractmethod" => true,
            "inlinemethod" => true,
            "introducevariable" => true,
            "organizeimports" => true,
            "addimport" => true,
            "encapsulatefield" => false,  // Not supported for TypeScript/JavaScript
            "renamesymbol" => true,
            _ => false
        };
    }

    private static RefactoringResult ConvertToRefactoringResult(
        TypeScriptMethodExtractor.TypeScriptExtractionResult result,
        string methodName,
        string filePath,
        string? accessModifier = null,
        bool? isStatic = null,
        string? returnType = null)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ExtractedMethod"] = result.ExtractedMethod ?? "",
            ["MethodCall"] = result.MethodCall ?? "",
            ["BackupId"] = result.BackupId ?? "",
            ["MethodName"] = methodName,
            ["FunctionType"] = "Function"
        };

        if (accessModifier != null) metadata["AccessModifier"] = accessModifier;
        if (isStatic.HasValue) metadata["IsStatic"] = isStatic.Value;
        if (returnType != null) metadata["ReturnType"] = returnType;

        return new RefactoringResult
        {
            Success = result.Success,
            Message = result.Success 
                ? $"Successfully extracted TypeScript method '{methodName}'" 
                : result.ErrorMessage ?? "TypeScript method extraction failed",
            Error = result.Success ? null : result.ErrorMessage,
            Changes = result.Success && !string.IsNullOrEmpty(result.ModifiedCode)
                ? [
                    new FileChange
                    {
                        FilePath = filePath,
                        OriginalContent = "", // Original content would need to be passed in
                        ModifiedContent = result.ModifiedCode,
                        ChangeType = "MethodExtraction"
                    }
                ]
                : [],
            FilesAffected = result.Success ? 1 : 0,
            Metadata = metadata
        };
    }

    private static RefactoringResult CreateErrorResult(string errorMessage)
    {
        return new RefactoringResult
        {
            Success = false,
            Error = errorMessage,
            Message = $"Operation failed: {errorMessage}",
            Changes = [],
            FilesAffected = 0,
            Metadata = new Dictionary<string, object>()
        };
    }

    private static RefactoringResult CreateNotSupportedResult(string message)
    {
        return new RefactoringResult
        {
            Success = false,
            Error = "Operation not supported",
            Message = message,
            Changes = [],
            FilesAffected = 0,
            Metadata = new Dictionary<string, object>
            {
                ["Status"] = "NotSupported",
                ["Reason"] = message
            }
        };
    }
}
