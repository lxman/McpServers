using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Tools.Common;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Tools;

/// <summary>
/// Focused class for C# refactoring operations following Single Responsibility Principle
/// </summary>
public class CSharpRefactoringTools : BaseToolClass
{
    private readonly IRefactoringOrchestrator _refactoringOrchestrator;
    private readonly ILogger<CSharpRefactoringTools> _logger;
    
    public CSharpRefactoringTools(IRefactoringOrchestrator refactoringOrchestrator, ILogger<CSharpRefactoringTools> logger)
    {
        _refactoringOrchestrator = refactoringOrchestrator ?? throw new ArgumentNullException(nameof(refactoringOrchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("CSharpRefactoringTools initialized successfully");
    }

    [McpServerTool]
    [Description("Rename a symbol across all files in the project")]
    public async Task<string> RefactorRenameSymbolAsync(
        [Description("Current symbol name to rename")]
        string symbolName,
        [Description("New name for the symbol")]
        string newName,
        [Description("Optional file path to limit search scope")]
        string? filePath = null,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        _logger.LogInformation("RefactorRenameSymbolAsync called: symbolName={SymbolName}, newName={NewName}, filePath={FilePath}, previewOnly={PreviewOnly}", 
            symbolName, newName, filePath, previewOnly);

        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(symbolName, nameof(symbolName));
            ValidateRequiredParameter(newName, nameof(newName));

            _logger.LogDebug("Calling orchestrator.RenameSymbolAsync");
            var result = await _refactoringOrchestrator.RenameSymbolAsync(filePath, symbolName, newName, previewOnly);
            _logger.LogDebug("Orchestrator returned: success={Success}, message={Message}", result.Success, result.Message);
            
            return result;
        });
    }

    [McpServerTool]
    [Description("Extract a method from selected lines of code")]
    public async Task<string> RefactorExtractMethodAsync(
        [Description("Path to the file containing the code to extract")]
        string filePath,
        [Description("Name for the new method")]
        string methodName,
        [Description("Starting line number (1-based)")]
        int startLine,
        [Description("Ending line number (1-based)")]
        int endLine,
        [Description("Access modifier for the new method")]
        string accessModifier = "private",
        [Description("Whether the method should be static")]
        bool isStatic = false,
        [Description("Return type for the new method (auto-detected if not specified)")]
        string? returnType = null,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        _logger.LogInformation("RefactorExtractMethodAsync called: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, accessModifier={AccessModifier}, isStatic={IsStatic}, returnType={ReturnType}, previewOnly={PreviewOnly}", 
            filePath, methodName, startLine, endLine, accessModifier, isStatic, returnType, previewOnly);

        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            _logger.LogDebug("Starting parameter validation");
            
            ValidateFilePath(filePath);
            ValidateRequiredParameter(methodName, nameof(methodName));
            ValidateLineNumber(startLine, nameof(startLine));
            ValidateLineNumber(endLine, nameof(endLine));

            if (endLine < startLine)
            {
                throw new ArgumentException("End line must be greater than or equal to start line.");
            }

            _logger.LogDebug("Parameter validation passed, calling orchestrator");

            // FIXED: Correct method signature with proper parameter order and CancellationToken
            var result = await _refactoringOrchestrator.ExtractMethodAsync(
                filePath, methodName, startLine, endLine, previewOnly, 
                accessModifier, isStatic, returnType, CancellationToken.None);
                
            _logger.LogDebug("Orchestrator returned: success={Success}, message={Message}, error={Error}", 
                result.Success, result.Message, result.Error);
                
            return result;
        });
    }

    [McpServerTool]
    [Description("Inline a method by replacing all call sites with the method body and removing the method definition")]
    public async Task<string> RefactorInlineMethodAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Name of the method to inline")]
        string methodName,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        _logger.LogInformation("RefactorInlineMethodAsync called: filePath={FilePath}, methodName={MethodName}, previewOnly={PreviewOnly}", 
            filePath, methodName, previewOnly);

        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateRequiredParameter(methodName, nameof(methodName));

            _logger.LogDebug("Calling orchestrator.InlineMethodAsync");
            var result = await _refactoringOrchestrator.InlineMethodAsync(filePath, methodName, previewOnly);
            _logger.LogDebug("Orchestrator returned: success={Success}, message={Message}", result.Success, result.Message);
            
            return result;
        });
    }

    [McpServerTool]
    [Description("Extract a selected expression into a local variable for better code readability and maintainability")]
    public async Task<string> RefactorIntroduceVariableAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Line number containing the expression (1-based)")]
        int line,
        [Description("Starting column of the expression (1-based)")]
        int startColumn,
        [Description("Ending column of the expression (1-based)")]
        int endColumn,
        [Description("Optional name for the new variable (auto-generated if not provided)")]
        string? variableName = null,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        _logger.LogInformation("RefactorIntroduceVariableAsync called: filePath={FilePath}, line={Line}, startColumn={StartColumn}, endColumn={EndColumn}, variableName={VariableName}, previewOnly={PreviewOnly}", 
            filePath, line, startColumn, endColumn, variableName, previewOnly);

        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateLineNumber(line);

            if (startColumn <= 0)
            {
                throw new ArgumentException("Start column must be positive (1-based).", nameof(startColumn));
            }

            if (endColumn <= 0)
            {
                throw new ArgumentException("End column must be positive (1-based).", nameof(endColumn));
            }

            if (endColumn <= startColumn)
            {
                throw new ArgumentException("End column must be greater than start column.");
            }

            _logger.LogDebug("Calling orchestrator.IntroduceVariableAsync");
            var result = await _refactoringOrchestrator.IntroduceVariableAsync(
                filePath, line, startColumn, endColumn, variableName, previewOnly);
            _logger.LogDebug("Orchestrator returned: success={Success}, message={Message}", result.Success, result.Message);
            
            return result;
        });
    }

    [McpServerTool]
    [Description("Convert public fields to private fields with public properties for better encapsulation")]
    public async Task<string> RefactorEncapsulateFieldAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Name of the field to encapsulate")]
        string fieldName,
        [Description("Optional name for the new property (auto-generated if not provided)")]
        string? propertyName = null,
        [Description("Use auto-property instead of full property with backing field")]
        bool useAutoProperty = true,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        _logger.LogInformation("RefactorEncapsulateFieldAsync called: filePath={FilePath}, fieldName={FieldName}, propertyName={PropertyName}, useAutoProperty={UseAutoProperty}, previewOnly={PreviewOnly}", 
            filePath, fieldName, propertyName, useAutoProperty, previewOnly);

        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateRequiredParameter(fieldName, nameof(fieldName));

            _logger.LogDebug("Calling orchestrator.EncapsulateFieldAsync");
            var result = await _refactoringOrchestrator.EncapsulateFieldAsync(
                filePath, fieldName, propertyName, useAutoProperty, previewOnly);
            _logger.LogDebug("Orchestrator returned: success={Success}, message={Message}", result.Success, result.Message);
            
            return result;
        });
    }

    [McpServerTool]
    [Description("Organize and sort using statements in a C# file")]
    public async Task<string> RefactorOrganizeImportsAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Remove unused using statements")]
        bool removeUnused = true,
        [Description("Sort using statements alphabetically")]
        bool sortAlphabetically = true,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        _logger.LogInformation("RefactorOrganizeImportsAsync called: filePath={FilePath}, removeUnused={RemoveUnused}, sortAlphabetically={SortAlphabetically}, previewOnly={PreviewOnly}", 
            filePath, removeUnused, sortAlphabetically, previewOnly);

        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);

            _logger.LogDebug("Calling orchestrator.OrganizeImportsAsync");
            var result = await _refactoringOrchestrator.OrganizeImportsAsync(filePath, removeUnused, sortAlphabetically, previewOnly);
            _logger.LogDebug("Orchestrator returned: success={Success}, message={Message}", result.Success, result.Message);
            
            return result;
        });
    }

    [McpServerTool]
    [Description("Add a using statement to a C# file")]
    public async Task<string> RefactorAddUsingAsync(
        [Description("Path to the C# file")]
        string filePath,
        [Description("Namespace to add (e.g., 'System.Collections.Generic')")]
        string usingNamespace,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        _logger.LogInformation("RefactorAddUsingAsync called: filePath={FilePath}, usingNamespace={UsingNamespace}, previewOnly={PreviewOnly}", 
            filePath, usingNamespace, previewOnly);

        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateRequiredParameter(usingNamespace, nameof(usingNamespace));

            _logger.LogDebug("Calling orchestrator.AddImportAsync");
            var result = await _refactoringOrchestrator.AddImportAsync(filePath, $"using {usingNamespace};", previewOnly);
            _logger.LogDebug("Orchestrator returned: success={Success}, message={Message}", result.Success, result.Message);
            
            return result;
        });
    }
}
