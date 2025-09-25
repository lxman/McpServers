using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Services.Refactoring;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Commands;

/// <summary>
/// Command for inlining methods by replacing call sites with method body.
/// Supports both C# and TypeScript method inlining.
/// </summary>
public class InlineMethodCommand : IRefactoringCommand
{
    private readonly ICSharpMethodInliner _csharpMethodInliner;
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly IPathValidationService _pathValidation;
    private readonly ILogger<InlineMethodCommand> _logger;

    public string CommandId => "inline-method";
    
    public string Description => "Inlines a method by replacing all call sites with the method body";
    
    public IEnumerable<LanguageType> SupportedLanguages => 
        [LanguageType.CSharp, LanguageType.TypeScript, LanguageType.JavaScript];

    public InlineMethodCommand(
        ICSharpMethodInliner csharpMethodInliner,
        ITypeScriptRefactoringService typeScriptRefactoringService,
        IPathValidationService pathValidation,
        ILogger<InlineMethodCommand> logger)
    {
        _csharpMethodInliner = csharpMethodInliner ?? throw new ArgumentNullException(nameof(csharpMethodInliner));
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

        if (!context.AdditionalData.ContainsKey("methodName"))
        {
            return CreateErrorResult("MethodName is required");
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
            return CreateErrorResult($"Method inlining not supported for {LanguageDetectionService.GetLanguageName(language)} files");
        }

        // Validate method name
        var methodName = context.AdditionalData["methodName"]?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return CreateErrorResult("MethodName cannot be empty");
        }

        return new RefactoringResult { Success = true, Message = "Validation passed" };
    }

    public async Task<RefactoringResult> ExecuteAsync(RefactoringContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("InlineMethodCommand executing for file: {FilePath}", context.FilePath);

        // Extract parameters
        var methodName = context.AdditionalData["methodName"]?.ToString() ?? throw new InvalidOperationException("MethodName is required");
        var previewOnly = bool.Parse(context.AdditionalData["previewOnly"]?.ToString() ?? "false");

        var validatedPath = _pathValidation.ValidateFileExists(context.FilePath);
        var language = LanguageDetectionService.DetectLanguage(validatedPath);

        _logger.LogInformation("Inlining method '{MethodName}' in {Language} file: {FilePath}", 
            methodName, language, validatedPath);

        try
        {
            return language switch
            {
                LanguageType.CSharp => await InlineCSharpMethodAsync(validatedPath, methodName, previewOnly, cancellationToken),
                LanguageType.TypeScript or LanguageType.JavaScript => await InlineTypeScriptMethodAsync(validatedPath, methodName, previewOnly, cancellationToken),
                _ => CreateErrorResult($"Method inlining not supported for {LanguageDetectionService.GetLanguageName(language)} files")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inlining method '{MethodName}' in file: {FilePath}", methodName, context.FilePath);
            return CreateErrorResult($"Failed to inline method: {ex.Message}");
        }
    }

    private async Task<RefactoringResult> InlineCSharpMethodAsync(
        string filePath,
        string methodName,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("InlineCSharpMethodAsync called with: filePath={FilePath}, methodName={MethodName}, previewOnly={PreviewOnly}", 
            filePath, methodName, previewOnly);

        var options = new MethodInliningOptions
        {
            MethodName = methodName
        };

        return await _csharpMethodInliner.InlineMethodAsync(filePath, options, previewOnly, cancellationToken);
    }

    private async Task<RefactoringResult> InlineTypeScriptMethodAsync(
        string filePath,
        string methodName,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("InlineTypeScriptMethodAsync called with: filePath={FilePath}, methodName={MethodName}, previewOnly={PreviewOnly}", 
            filePath, methodName, previewOnly);

        return await _typeScriptRefactoringService.InlineFunctionAsync(filePath, methodName, "file", previewOnly, cancellationToken);
    }

    private static RefactoringResult CreateErrorResult(string errorMessage)
    {
        return new RefactoringResult
        {
            Success = false,
            Error = errorMessage,
            Message = $"Inline method operation failed: {errorMessage}",
            Changes = [],
            FilesAffected = 0,
            Metadata = new Dictionary<string, object>()
        };
    }
}