using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Services.Refactoring;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Strategies;

/// <summary>
/// C# language-specific refactoring strategy implementation.
/// Handles all C# refactoring operations using dedicated C# services.
/// </summary>
public class CSharpRefactoringStrategy : ILanguageRefactoringStrategy
{
    private readonly ICSharpMethodExtractor _methodExtractor;
    private readonly ICSharpImportManager _importManager;
    private readonly ICSharpVariableOperations _variableOperations;
    private readonly ICSharpMethodInliner _methodInliner;
    private readonly SymbolRenameService _symbolRenameService;
    private readonly ILogger<CSharpRefactoringStrategy> _logger;

    public LanguageType Language => LanguageType.CSharp;

    public CSharpRefactoringStrategy(
        ICSharpMethodExtractor methodExtractor,
        ICSharpImportManager importManager,
        ICSharpVariableOperations variableOperations,
        ICSharpMethodInliner methodInliner,
        SymbolRenameService symbolRenameService,
        ILogger<CSharpRefactoringStrategy> logger)
    {
        _methodExtractor = methodExtractor ?? throw new ArgumentNullException(nameof(methodExtractor));
        _importManager = importManager ?? throw new ArgumentNullException(nameof(importManager));
        _variableOperations = variableOperations ?? throw new ArgumentNullException(nameof(variableOperations));
        _methodInliner = methodInliner ?? throw new ArgumentNullException(nameof(methodInliner));
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
        _logger.LogDebug("C# ExtractMethodAsync (basic) called: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}",
            filePath, methodName, startLine, endLine, previewOnly);

        var context = new RefactoringContext
        {
            FilePath = filePath,
            Language = LanguageType.CSharp
        };

        var options = new CSharpExtractionOptions
        {
            NewMethodName = methodName,
            StartLine = startLine,
            EndLine = endLine,
            AccessModifier = "private",
            IsStatic = false
        };

        context.AdditionalData["extractionOptions"] = options;
        context.AdditionalData["previewOnly"] = previewOnly;

        try
        {
            var result = await _methodExtractor.ExecuteAsync(context);
            _logger.LogDebug("C# method extraction returned: success={Success}, message={Message}", result.Success, result.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# method extraction");
            return CreateErrorResult($"C# method extraction failed: {ex.Message}");
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
        _logger.LogDebug("C# ExtractMethodAsync (advanced) called: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}, accessModifier={AccessModifier}, isStatic={IsStatic}, returnType={ReturnType}",
            filePath, methodName, startLine, endLine, previewOnly, accessModifier, isStatic, returnType);

        var context = new RefactoringContext
        {
            FilePath = filePath,
            Language = LanguageType.CSharp
        };

        var options = new CSharpExtractionOptions
        {
            NewMethodName = methodName,
            StartLine = startLine,
            EndLine = endLine,
            AccessModifier = accessModifier,
            IsStatic = isStatic,
            ReturnType = returnType
        };

        context.AdditionalData["extractionOptions"] = options;
        context.AdditionalData["previewOnly"] = previewOnly;

        try
        {
            var result = await _methodExtractor.ExecuteAsync(context);
            _logger.LogDebug("C# method extraction (advanced) returned: success={Success}, message={Message}", result.Success, result.Message);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# method extraction (advanced)");
            return CreateErrorResult($"C# method extraction failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> InlineMethodAsync(
        string filePath,
        string methodName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("C# InlineMethodAsync called: filePath={FilePath}, methodName={MethodName}, previewOnly={PreviewOnly}",
            filePath, methodName, previewOnly);

        try
        {
            var options = new MethodInliningOptions
            {
                MethodName = methodName
            };

            return await _methodInliner.InlineMethodAsync(filePath, options, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# method inlining");
            return CreateErrorResult($"C# method inlining failed: {ex.Message}");
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
        _logger.LogDebug("C# IntroduceVariableAsync called: filePath={FilePath}, line={Line}, startColumn={StartColumn}, endColumn={EndColumn}, variableName={VariableName}, previewOnly={PreviewOnly}",
            filePath, line, startColumn, endColumn, variableName, previewOnly);

        try
        {
            var options = new VariableIntroductionOptions
            {
                Line = line,
                StartColumn = startColumn,
                EndColumn = endColumn,
                VariableName = variableName
            };

            return await _variableOperations.IntroduceVariableAsync(filePath, options, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# variable introduction");
            return CreateErrorResult($"C# variable introduction failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool removeUnused = true,
        bool sortAlphabetically = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("C# OrganizeImportsAsync called: filePath={FilePath}, removeUnused={RemoveUnused}, sortAlphabetically={SortAlphabetically}, previewOnly={PreviewOnly}",
            filePath, removeUnused, sortAlphabetically, previewOnly);

        try
        {
            var options = new CSharpImportOperation
            {
                RemoveDuplicates = true,
                RemoveUnused = removeUnused,
                SortAlphabetically = sortAlphabetically,
                GroupByType = true,
                SeparateSystemNamespaces = true
            };

            return await _importManager.OrganizeImportsAsync(filePath, options, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# import organization");
            return CreateErrorResult($"C# import organization failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("C# AddImportAsync called: filePath={FilePath}, importStatement={ImportStatement}, previewOnly={PreviewOnly}",
            filePath, importStatement, previewOnly);

        try
        {
            // Extract namespace from "using System.Collections;" format
            var usingNamespace = importStatement.Replace("using ", "").Replace(";", "").Trim();
            return await _importManager.AddUsingAsync(filePath, usingNamespace, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# import addition");
            return CreateErrorResult($"C# import addition failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> EncapsulateFieldAsync(
        string filePath,
        string fieldName,
        string? propertyName = null,
        bool useAutoProperty = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("C# EncapsulateFieldAsync called: filePath={FilePath}, fieldName={FieldName}, propertyName={PropertyName}, useAutoProperty={UseAutoProperty}, previewOnly={PreviewOnly}",
            filePath, fieldName, propertyName, useAutoProperty, previewOnly);

        try
        {
            var options = new FieldEncapsulationOptions
            {
                FieldName = fieldName,
                PropertyName = propertyName,
                UseAutoProperty = useAutoProperty
            };

            return await _variableOperations.EncapsulateFieldAsync(filePath, options, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# field encapsulation");
            return CreateErrorResult($"C# field encapsulation failed: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> RenameSymbolAsync(
        string? filePath,
        string symbolName,
        string newName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("C# RenameSymbolAsync called: filePath={FilePath}, symbolName={SymbolName}, newName={NewName}, previewOnly={PreviewOnly}",
            filePath, symbolName, newName, previewOnly);

        try
        {
            if (filePath != null)
            {
                return await _symbolRenameService.RenameSymbolAsync(symbolName, newName, filePath, previewOnly, cancellationToken);
            }
            else
            {
                // Project-wide rename
                return await _symbolRenameService.RenameSymbolAsync("", symbolName, newName, previewOnly, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in C# symbol rename");
            return CreateErrorResult($"C# symbol rename failed: {ex.Message}");
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
            "encapsulatefield" => true,  // C# specific
            "renamesymbol" => true,
            _ => false
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
}
