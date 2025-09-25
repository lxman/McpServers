using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Services.Refactoring;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Commands;

/// <summary>
/// Command for introducing variables by extracting expressions into local variables.
/// Supports both C# and TypeScript variable introduction.
/// </summary>
public class IntroduceVariableCommand : IRefactoringCommand
{
    private readonly ICSharpVariableOperations _csharpVariableOperations;
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly IPathValidationService _pathValidation;
    private readonly ILogger<IntroduceVariableCommand> _logger;

    public string CommandId => "introduce-variable";
    
    public string Description => "Introduces a variable by extracting the selected expression into a local variable";
    
    public IEnumerable<LanguageType> SupportedLanguages => 
        [LanguageType.CSharp, LanguageType.TypeScript, LanguageType.JavaScript];

    public IntroduceVariableCommand(
        ICSharpVariableOperations csharpVariableOperations,
        ITypeScriptRefactoringService typeScriptRefactoringService,
        IPathValidationService pathValidation,
        ILogger<IntroduceVariableCommand> logger)
    {
        _csharpVariableOperations = csharpVariableOperations ?? throw new ArgumentNullException(nameof(csharpVariableOperations));
        _typeScriptRefactoringService = typeScriptRefactoringService ?? throw new ArgumentNullException(nameof(typeScriptRefactoringService));
        _pathValidation = pathValidation ?? throw new ArgumentNullException(nameof(pathValidation));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool SupportsFile(string filePath)
    {
        var language = LanguageDetectionService.DetectLanguage(filePath);
        return SupportedLanguages.Contains(language);
    }

    public async Task<RefactoringResult> ValidateAsync(RefactoringContext context)
    {
        // Validate required parameters
        if (string.IsNullOrEmpty(context.FilePath))
        {
            return CreateErrorResult("FilePath is required");
        }

        if (!context.AdditionalData.ContainsKey("line") || 
            !context.AdditionalData.ContainsKey("startColumn") || 
            !context.AdditionalData.ContainsKey("endColumn"))
        {
            return CreateErrorResult("Line, StartColumn, and EndColumn are required");
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
            var language = LanguageDetectionService.DetectLanguage(context.FilePath);
            return CreateErrorResult($"Variable introduction not supported for {LanguageDetectionService.GetLanguageName(language)} files");
        }

        // Validate line and column numbers
        if (!int.TryParse(context.AdditionalData["line"]?.ToString(), out var line) ||
            !int.TryParse(context.AdditionalData["startColumn"]?.ToString(), out var startColumn) ||
            !int.TryParse(context.AdditionalData["endColumn"]?.ToString(), out var endColumn))
        {
            return CreateErrorResult("Line, StartColumn, and EndColumn must be valid integers");
        }

        if (line <= 0 || startColumn <= 0 || endColumn <= 0 || startColumn > endColumn)
        {
            return CreateErrorResult("Line and column numbers must be positive with StartColumn <= EndColumn");
        }

        return new RefactoringResult { Success = true, Message = "Validation passed" };
    }

    public async Task<RefactoringResult> ExecuteAsync(RefactoringContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("IntroduceVariableCommand executing for file: {FilePath}", context.FilePath);

        // Extract parameters
        var line = int.Parse(context.AdditionalData["line"]?.ToString() ?? "0");
        var startColumn = int.Parse(context.AdditionalData["startColumn"]?.ToString() ?? "0");
        var endColumn = int.Parse(context.AdditionalData["endColumn"]?.ToString() ?? "0");
        var variableName = context.AdditionalData["variableName"]?.ToString();
        var previewOnly = bool.Parse(context.AdditionalData["previewOnly"]?.ToString() ?? "false");

        var validatedPath = _pathValidation.ValidateFileExists(context.FilePath);
        var language = LanguageDetectionService.DetectLanguage(validatedPath);

        _logger.LogInformation("Introducing variable in {Language} file: {FilePath} at line {Line}, columns {StartColumn}-{EndColumn}", 
            language, validatedPath, line, startColumn, endColumn);

        try
        {
            return language switch
            {
                LanguageType.CSharp => await IntroduceCSharpVariableAsync(validatedPath, line, startColumn, endColumn, variableName, previewOnly, cancellationToken),
                LanguageType.TypeScript or LanguageType.JavaScript => await IntroduceTypeScriptVariableAsync(validatedPath, line, startColumn, endColumn, variableName, previewOnly, cancellationToken),
                _ => CreateErrorResult($"Variable introduction not supported for {LanguageDetectionService.GetLanguageName(language)} files")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error introducing variable in file: {FilePath}", context.FilePath);
            return CreateErrorResult($"Failed to introduce variable: {ex.Message}");
        }
    }

    private async Task<RefactoringResult> IntroduceCSharpVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("IntroduceCSharpVariableAsync called with: filePath={FilePath}, line={Line}, startColumn={StartColumn}, endColumn={EndColumn}, variableName={VariableName}, previewOnly={PreviewOnly}", 
            filePath, line, startColumn, endColumn, variableName, previewOnly);

        var options = new VariableIntroductionOptions
        {
            Line = line,
            StartColumn = startColumn,
            EndColumn = endColumn,
            VariableName = variableName
        };

        return await _csharpVariableOperations.IntroduceVariableAsync(filePath, options, previewOnly, cancellationToken);
    }

    private async Task<RefactoringResult> IntroduceTypeScriptVariableAsync(
        string filePath,
        int line,
        int startColumn,
        int endColumn,
        string? variableName,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("IntroduceTypeScriptVariableAsync called with: filePath={FilePath}, line={Line}, startColumn={StartColumn}, endColumn={EndColumn}, variableName={VariableName}, previewOnly={PreviewOnly}", 
            filePath, line, startColumn, endColumn, variableName, previewOnly);

        // Default to 'const' for TypeScript variables
        var declarationType = "const";
        
        return await _typeScriptRefactoringService.IntroduceVariableAsync(filePath, line, startColumn, endColumn, variableName, declarationType, previewOnly, cancellationToken);
    }

    private static RefactoringResult CreateErrorResult(string errorMessage)
    {
        return new RefactoringResult
        {
            Success = false,
            Error = errorMessage,
            Message = $"Introduce variable operation failed: {errorMessage}",
            Changes = [],
            FilesAffected = 0,
            Metadata = new Dictionary<string, object>()
        };
    }
}