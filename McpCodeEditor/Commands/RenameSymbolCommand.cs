using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Services.Refactoring;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Commands;

/// <summary>
/// Command for renaming symbols across files.
/// Supports both C# and TypeScript symbol renaming with project-wide scope.
/// </summary>
public class RenameSymbolCommand : IRefactoringCommand
{
    private readonly SymbolRenameService _symbolRenameService;
    private readonly TypeScriptSymbolRenameService _typeScriptSymbolRenameService;
    private readonly IPathValidationService _pathValidation;
    private readonly ILogger<RenameSymbolCommand> _logger;

    public string CommandId => "rename-symbol";
    
    public string Description => "Renames a symbol across all files in the project or within a specific file scope";
    
    public IEnumerable<LanguageType> SupportedLanguages => 
        [LanguageType.CSharp, LanguageType.TypeScript, LanguageType.JavaScript];

    public RenameSymbolCommand(
        SymbolRenameService symbolRenameService,
        TypeScriptSymbolRenameService typeScriptSymbolRenameService,
        IPathValidationService pathValidation,
        ILogger<RenameSymbolCommand> logger)
    {
        _symbolRenameService = symbolRenameService ?? throw new ArgumentNullException(nameof(symbolRenameService));
        _typeScriptSymbolRenameService = typeScriptSymbolRenameService ?? throw new ArgumentNullException(nameof(typeScriptSymbolRenameService));
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
        if (!context.AdditionalData.ContainsKey("symbolName") || !context.AdditionalData.ContainsKey("newName"))
        {
            return CreateErrorResult("SymbolName and NewName are required");
        }

        // Validate symbol names
        string symbolName = context.AdditionalData["symbolName"]?.ToString() ?? "";
        string newName = context.AdditionalData["newName"]?.ToString() ?? "";
        
        if (string.IsNullOrWhiteSpace(symbolName))
        {
            return CreateErrorResult("SymbolName cannot be empty");
        }
        
        if (string.IsNullOrWhiteSpace(newName))
        {
            return CreateErrorResult("NewName cannot be empty");
        }

        if (symbolName == newName)
        {
            return CreateErrorResult("NewName must be different from the current SymbolName");
        }

        // Validate file if provided (optional for project-wide rename)
        if (!string.IsNullOrEmpty(context.FilePath))
        {
            try
            {
                _pathValidation.ValidateFileExists(context.FilePath);
            }
            catch (Exception ex)
            {
                return CreateErrorResult($"File validation failed: {ex.Message}");
            }

            // Validate language support if file is specified
            if (!SupportsFile(context.FilePath))
            {
                LanguageType language = LanguageDetectionService.DetectLanguage(context.FilePath);
                return CreateErrorResult($"Symbol renaming not supported for {LanguageDetectionService.GetLanguageName(language)} files");
            }
        }

        return new RefactoringResult { Success = true, Message = "Validation passed" };
    }

    public async Task<RefactoringResult> ExecuteAsync(RefactoringContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RenameSymbolCommand executing for file: {FilePath}", context.FilePath ?? "project-wide");

        // Extract parameters
        string symbolName = context.AdditionalData["symbolName"]?.ToString() ?? throw new InvalidOperationException("SymbolName is required");
        string newName = context.AdditionalData["newName"]?.ToString() ?? throw new InvalidOperationException("NewName is required");
        bool previewOnly = bool.Parse(context.AdditionalData["previewOnly"]?.ToString() ?? "false");

        try
        {
            if (!string.IsNullOrEmpty(context.FilePath))
            {
                // File-scoped rename
                string validatedPath = _pathValidation.ValidateFileExists(context.FilePath);
                LanguageType language = LanguageDetectionService.DetectLanguage(validatedPath);

                _logger.LogInformation("Renaming symbol '{SymbolName}' to '{NewName}' in {Language} file: {FilePath}", 
                    symbolName, newName, language, validatedPath);

                return language switch
                {
                    LanguageType.CSharp => await _symbolRenameService.RenameSymbolAsync(symbolName, newName, validatedPath, previewOnly, cancellationToken),
                    LanguageType.TypeScript or LanguageType.JavaScript => await _typeScriptSymbolRenameService.RenameSymbolAsync(symbolName, newName, validatedPath, previewOnly, cancellationToken),
                    _ => CreateErrorResult($"Symbol renaming not supported for {LanguageDetectionService.GetLanguageName(language)} files")
                };
            }
            else
            {
                // Project-wide rename (defaults to C# for project-wide operations)
                _logger.LogInformation("Renaming symbol '{SymbolName}' to '{NewName}' project-wide", symbolName, newName);
                return await _symbolRenameService.RenameSymbolAsync("", symbolName, newName, previewOnly, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming symbol '{SymbolName}' to '{NewName}'", symbolName, newName);
            return CreateErrorResult($"Failed to rename symbol: {ex.Message}");
        }
    }

    private static RefactoringResult CreateErrorResult(string errorMessage)
    {
        return new RefactoringResult
        {
            Success = false,
            Error = errorMessage,
            Message = $"Rename symbol operation failed: {errorMessage}",
            Changes = [],
            FilesAffected = 0,
            Metadata = new Dictionary<string, object>()
        };
    }
}