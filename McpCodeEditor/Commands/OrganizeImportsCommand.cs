using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Services.Refactoring;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Commands;

/// <summary>
/// Command for organizing imports/using statements.
/// Supports organizing existing imports and adding new import statements.
/// Works with both C# and TypeScript files.
/// </summary>
public class OrganizeImportsCommand : IRefactoringCommand
{
    private readonly ICSharpImportManager _csharpImportManager;
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly IPathValidationService _pathValidation;
    private readonly ILogger<OrganizeImportsCommand> _logger;

    public string CommandId => "organize-imports";
    
    public string Description => "Organizes imports by sorting alphabetically, removing unused imports, and optionally adding new import statements";
    
    public IEnumerable<LanguageType> SupportedLanguages => 
        [LanguageType.CSharp, LanguageType.TypeScript, LanguageType.JavaScript];

    public OrganizeImportsCommand(
        ICSharpImportManager csharpImportManager,
        ITypeScriptRefactoringService typeScriptRefactoringService,
        IPathValidationService pathValidation,
        ILogger<OrganizeImportsCommand> logger)
    {
        _csharpImportManager = csharpImportManager ?? throw new ArgumentNullException(nameof(csharpImportManager));
        _typeScriptRefactoringService = typeScriptRefactoringService ?? throw new ArgumentNullException(nameof(typeScriptRefactoringService));
        _pathValidation = pathValidation ?? throw new ArgumentNullException(nameof(pathValidation));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool SupportsFile(string filePath)
    {
        LanguageType language = LanguageDetectionService.DetectLanguage(filePath);
        return SupportedLanguages.Contains(language);
    }

    public async Task<RefactoringResult> ValidateAsync(RefactoringContext context)
    {
        // Validate required parameters
        if (string.IsNullOrEmpty(context.FilePath))
        {
            return CreateErrorResult("FilePath is required");
        }

        // Validate file exists
        try
        {
            _pathValidation.ValidateFileExists(context.FilePath);
        }
        catch (Exception ex)
        {
            return CreateErrorResult($"File validation failed: {ex.Message}");
        }

        // Validate language support
        if (!SupportsFile(context.FilePath))
        {
            LanguageType language = LanguageDetectionService.DetectLanguage(context.FilePath);
            return CreateErrorResult($"Import organization not supported for {LanguageDetectionService.GetLanguageName(language)} files");
        }

        return new RefactoringResult { Success = true, Message = "Validation passed" };
    }

    public async Task<RefactoringResult> ExecuteAsync(RefactoringContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OrganizeImportsCommand executing for file: {FilePath}", context.FilePath);

        // Extract parameters with defaults
        bool removeUnused = bool.Parse(context.AdditionalData.GetValueOrDefault("removeUnused")?.ToString() ?? "true");
        bool sortAlphabetically = bool.Parse(context.AdditionalData.GetValueOrDefault("sortAlphabetically")?.ToString() ?? "true");
        bool previewOnly = bool.Parse(context.AdditionalData.GetValueOrDefault("previewOnly")?.ToString() ?? "false");
        var importStatement = context.AdditionalData.GetValueOrDefault("importStatement")?.ToString();

        string validatedPath = _pathValidation.ValidateFileExists(context.FilePath);
        LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);

        _logger.LogInformation("Organizing imports in {Language} file: {FilePath} (removeUnused={RemoveUnused}, sort={Sort}, addImport={ImportStatement})", 
            language, validatedPath, removeUnused, sortAlphabetically, importStatement);

        try
        {
            return language switch
            {
                LanguageType.CSharp => await OrganizeCSharpImportsAsync(validatedPath, removeUnused, sortAlphabetically, importStatement, previewOnly, cancellationToken),
                LanguageType.TypeScript or LanguageType.JavaScript => await OrganizeTypeScriptImportsAsync(validatedPath, removeUnused, sortAlphabetically, importStatement, previewOnly, cancellationToken),
                _ => CreateErrorResult($"Import organization not supported for {LanguageDetectionService.GetLanguageName(language)} files")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error organizing imports in file: {FilePath}", context.FilePath);
            return CreateErrorResult($"Failed to organize imports: {ex.Message}");
        }
    }

    private async Task<RefactoringResult> OrganizeCSharpImportsAsync(
        string filePath,
        bool removeUnused,
        bool sortAlphabetically,
        string? importStatement,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("OrganizeCSharpImportsAsync called with: filePath={FilePath}, removeUnused={RemoveUnused}, sort={Sort}, importStatement={ImportStatement}, previewOnly={PreviewOnly}", 
            filePath, removeUnused, sortAlphabetically, importStatement, previewOnly);

        // If we have an import statement to add, add it first
        if (!string.IsNullOrEmpty(importStatement))
        {
            // Extract namespace from "using System.Collections;" format
            string usingNamespace = importStatement.Replace("using ", "").Replace(";", "").Trim();
            RefactoringResult addResult = await _csharpImportManager.AddUsingAsync(filePath, usingNamespace, previewOnly, cancellationToken);
            
            if (!addResult.Success)
            {
                return addResult;
            }
        }

        // Then organize imports
        var options = new CSharpImportOperation
        {
            RemoveDuplicates = true,
            RemoveUnused = removeUnused,
            SortAlphabetically = sortAlphabetically,
            GroupByType = true,
            SeparateSystemNamespaces = true
        };

        return await _csharpImportManager.OrganizeImportsAsync(filePath, options, previewOnly, cancellationToken);
    }

    private async Task<RefactoringResult> OrganizeTypeScriptImportsAsync(
        string filePath,
        bool removeUnused,
        bool sortAlphabetically,
        string? importStatement,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("OrganizeTypeScriptImportsAsync called with: filePath={FilePath}, removeUnused={RemoveUnused}, sort={Sort}, importStatement={ImportStatement}, previewOnly={PreviewOnly}", 
            filePath, removeUnused, sortAlphabetically, importStatement, previewOnly);

        // If we have an import statement to add, add it first
        if (!string.IsNullOrEmpty(importStatement))
        {
            RefactoringResult addResult = await _typeScriptRefactoringService.AddImportAsync(filePath, importStatement, previewOnly, cancellationToken);
            
            if (!addResult.Success)
            {
                return addResult;
            }
        }

        // Then organize imports
        return await _typeScriptRefactoringService.OrganizeImportsAsync(filePath, sortAlphabetically, true, removeUnused, previewOnly, cancellationToken);
    }

    private static RefactoringResult CreateErrorResult(string errorMessage)
    {
        return new RefactoringResult
        {
            Success = false,
            Error = errorMessage,
            Message = $"Organize imports operation failed: {errorMessage}",
            Changes = [],
            FilesAffected = 0,
            Metadata = new Dictionary<string, object>()
        };
    }
}