using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.ServiceModules;
using McpCodeEditor.Strategies;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.Refactoring;

/// <summary>
/// Main orchestrator for coordinating refactoring operations across different languages.
/// Simplified implementation using Strategy and Command patterns (Phase 3 refactoring).
/// Now acts as a lightweight coordinator that delegates to focused services.
/// </summary>
public class RefactoringOrchestrator : IRefactoringOrchestrator
{
    private readonly LanguageDetectionService _languageDetection;
    private readonly IPathValidationService _pathValidation;
    private readonly ILanguageRefactoringStrategyFactory _strategyFactory;
    private readonly ICommandFactory _commandFactory;
    private readonly ILogger<RefactoringOrchestrator> _logger;

    /// <summary>
    /// Simplified constructor with only essential dependencies.
    /// Strategy and Command patterns eliminate the need for direct service injection.
    /// </summary>
    public RefactoringOrchestrator(
        LanguageDetectionService languageDetection,
        IPathValidationService pathValidation,
        ILanguageRefactoringStrategyFactory strategyFactory,
        ICommandFactory commandFactory,
        ILogger<RefactoringOrchestrator> logger)
    {
        _languageDetection = languageDetection ?? throw new ArgumentNullException(nameof(languageDetection));
        _pathValidation = pathValidation ?? throw new ArgumentNullException(nameof(pathValidation));
        _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
        _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("RefactoringOrchestrator initialized with simplified strategy/command pattern architecture");
    }

    #region Extract Method Operations

    public async Task<RefactoringResult> ExtractMethodAsync(
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ExtractMethodAsync (basic) called: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}", 
            filePath, methodName, startLine, endLine, previewOnly);

        try
        {
            string validatedPath = _pathValidation.ValidateFileExists(filePath);
            LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
            
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            if (strategy == null)
            {
                return CreateErrorResult($"Method extraction not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }

            _logger.LogInformation("Extracting method '{MethodName}' from {Language} file: {FilePath}", 
                methodName, language, validatedPath);

            return await strategy.ExtractMethodAsync(validatedPath, methodName, startLine, endLine, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting method '{MethodName}' from file: {FilePath}", methodName, filePath);
            return CreateErrorResult($"Failed to extract method: {ex.Message}");
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
        _logger.LogInformation("ExtractMethodAsync (advanced) called: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}, accessModifier={AccessModifier}, isStatic={IsStatic}, returnType={ReturnType}", 
            filePath, methodName, startLine, endLine, previewOnly, accessModifier, isStatic, returnType);

        try
        {
            string validatedPath = _pathValidation.ValidateFileExists(filePath);
            LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
            
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            if (strategy == null)
            {
                return CreateErrorResult($"Method extraction not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }

            _logger.LogInformation("Extracting method '{MethodName}' from {Language} file: {FilePath} with advanced options", 
                methodName, language, validatedPath);

            return await strategy.ExtractMethodAsync(validatedPath, methodName, startLine, endLine, previewOnly, accessModifier, isStatic, returnType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting method '{MethodName}' from file: {FilePath}", methodName, filePath);
            return CreateErrorResult($"Failed to extract method: {ex.Message}");
        }
    }

    #endregion

    #region Inline Method Operations

    public async Task<RefactoringResult> InlineMethodAsync(
        string filePath,
        string methodName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("InlineMethodAsync called: filePath={FilePath}, methodName={MethodName}, previewOnly={PreviewOnly}", 
            filePath, methodName, previewOnly);

        try
        {
            string validatedPath = _pathValidation.ValidateFileExists(filePath);
            LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
            
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            if (strategy == null)
            {
                return CreateErrorResult($"Method inlining not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }

            _logger.LogInformation("Inlining method '{MethodName}' in {Language} file: {FilePath}", 
                methodName, language, validatedPath);

            return await strategy.InlineMethodAsync(validatedPath, methodName, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inlining method '{MethodName}' in file: {FilePath}", methodName, filePath);
            return CreateErrorResult($"Failed to inline method: {ex.Message}");
        }
    }

    #endregion

    #region Variable Introduction Operations

    public async Task<RefactoringResult> IntroduceVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName = null,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("IntroduceVariableAsync called: filePath={FilePath}, line={Line}, startColumn={StartColumn}, endColumn={EndColumn}, variableName={VariableName}, previewOnly={PreviewOnly}", 
            filePath, line, startColumn, endColumn, variableName, previewOnly);

        try
        {
            string validatedPath = _pathValidation.ValidateFileExists(filePath);
            LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
            
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            if (strategy == null)
            {
                return CreateErrorResult($"Variable introduction not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }

            _logger.LogInformation("Introducing variable in {Language} file: {FilePath} at line {Line}", 
                language, validatedPath, line);

            return await strategy.IntroduceVariableAsync(validatedPath, line, startColumn, endColumn, variableName, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error introducing variable in file: {FilePath}", filePath);
            return CreateErrorResult($"Failed to introduce variable: {ex.Message}");
        }
    }

    #endregion

    #region Import Organization Operations

    public async Task<RefactoringResult> OrganizeImportsAsync(
        string filePath,
        bool removeUnused = true,
        bool sortAlphabetically = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OrganizeImportsAsync called: filePath={FilePath}, removeUnused={RemoveUnused}, sortAlphabetically={SortAlphabetically}, previewOnly={PreviewOnly}", 
            filePath, removeUnused, sortAlphabetically, previewOnly);

        try
        {
            string validatedPath = _pathValidation.ValidateFileExists(filePath);
            LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
            
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            if (strategy == null)
            {
                return CreateErrorResult($"Import organization not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }

            _logger.LogInformation("Organizing imports in {Language} file: {FilePath}", language, validatedPath);

            return await strategy.OrganizeImportsAsync(validatedPath, removeUnused, sortAlphabetically, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error organizing imports in file: {FilePath}", filePath);
            return CreateErrorResult($"Failed to organize imports: {ex.Message}");
        }
    }

    public async Task<RefactoringResult> AddImportAsync(
        string filePath,
        string importStatement,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AddImportAsync called: filePath={FilePath}, importStatement={ImportStatement}, previewOnly={PreviewOnly}", 
            filePath, importStatement, previewOnly);

        try
        {
            string validatedPath = _pathValidation.ValidateFileExists(filePath);
            LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
            
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            if (strategy == null)
            {
                return CreateErrorResult($"Import addition not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }

            _logger.LogInformation("Adding import to {Language} file: {FilePath}", language, validatedPath);

            return await strategy.AddImportAsync(validatedPath, importStatement, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding import to file: {FilePath}", filePath);
            return CreateErrorResult($"Failed to add import: {ex.Message}");
        }
    }

    #endregion

    #region Field Encapsulation Operations

    public async Task<RefactoringResult> EncapsulateFieldAsync(
        string filePath,
        string fieldName,
        string? propertyName = null,
        bool useAutoProperty = true,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EncapsulateFieldAsync called: filePath={FilePath}, fieldName={FieldName}, propertyName={PropertyName}, useAutoProperty={UseAutoProperty}, previewOnly={PreviewOnly}", 
            filePath, fieldName, propertyName, useAutoProperty, previewOnly);

        try
        {
            string validatedPath = _pathValidation.ValidateFileExists(filePath);
            LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
            
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            if (strategy == null || !strategy.IsOperationSupported("encapsulateField"))
            {
                return CreateErrorResult($"Field encapsulation not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }

            _logger.LogInformation("Encapsulating field '{FieldName}' in {Language} file: {FilePath}", fieldName, language, validatedPath);

            return await strategy.EncapsulateFieldAsync(validatedPath, fieldName, propertyName, useAutoProperty, previewOnly, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encapsulating field '{FieldName}' in file: {FilePath}", fieldName, filePath);
            return CreateErrorResult($"Failed to encapsulate field: {ex.Message}");
        }
    }

    #endregion

    #region Symbol Renaming Operations

    public async Task<RefactoringResult> RenameSymbolAsync(
        string? filePath,
        string symbolName,
        string newName,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RenameSymbolAsync called: filePath={FilePath}, symbolName={SymbolName}, newName={NewName}, previewOnly={PreviewOnly}", 
            filePath, symbolName, newName, previewOnly);

        try
        {
            if (filePath != null)
            {
                string validatedPath = _pathValidation.ValidateFileExists(filePath);
                LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);
                
                ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
                if (strategy == null)
                {
                    return CreateErrorResult($"Symbol renaming not supported for {LanguageDetectionService.GetLanguageName(language)} files");
                }

                _logger.LogInformation("Renaming symbol '{SymbolName}' to '{NewName}' in {Language} file: {FilePath}", 
                    symbolName, newName, language, validatedPath);

                return await strategy.RenameSymbolAsync(validatedPath, symbolName, newName, previewOnly, cancellationToken);
            }
            else
            {
                // Project-wide rename - default to C# strategy
                ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(LanguageType.CSharp);
                if (strategy == null)
                {
                    return CreateErrorResult("Project-wide symbol renaming not available");
                }

                _logger.LogInformation("Renaming symbol '{SymbolName}' to '{NewName}' project-wide", symbolName, newName);
                return await strategy.RenameSymbolAsync(null, symbolName, newName, previewOnly, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming symbol '{SymbolName}' to '{NewName}'", symbolName, newName);
            return CreateErrorResult($"Failed to rename symbol: {ex.Message}");
        }
    }

    #endregion

    #region Capability and Support Methods

    public IEnumerable<LanguageType> GetSupportedLanguages()
    {
        return _strategyFactory.GetAllStrategies().Select(s => s.Language).Distinct();
    }

    public bool IsOperationSupported(string filePath, string operationType)
    {
        try
        {
            LanguageType language = LanguageDetectionService.DetectLanguage(filePath);
            ILanguageRefactoringStrategy? strategy = _strategyFactory.GetStrategy(language);
            
            return strategy?.IsOperationSupported(operationType) == true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
