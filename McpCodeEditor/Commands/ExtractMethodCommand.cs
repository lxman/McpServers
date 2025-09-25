using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Models.TypeScript;
using McpCodeEditor.Services.Refactoring;
using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Commands;

/// <summary>
/// Command for extracting methods from selected code blocks.
/// Supports both C# and TypeScript with basic and advanced extraction options.
/// </summary>
public class ExtractMethodCommand : IRefactoringCommand
{
    private readonly ICSharpMethodExtractor _csharpMethodExtractor;
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly IPathValidationService _pathValidation;
    private readonly ILogger<ExtractMethodCommand> _logger;

    public string CommandId => "extract-method";
    
    public string Description => "Extracts a method from the selected lines of code with specified parameters";
    
    public IEnumerable<LanguageType> SupportedLanguages => 
        [LanguageType.CSharp, LanguageType.TypeScript, LanguageType.JavaScript];

    public ExtractMethodCommand(
        ICSharpMethodExtractor csharpMethodExtractor,
        ITypeScriptRefactoringService typeScriptRefactoringService,
        IPathValidationService pathValidation,
        ILogger<ExtractMethodCommand> logger)
    {
        _csharpMethodExtractor = csharpMethodExtractor ?? throw new ArgumentNullException(nameof(csharpMethodExtractor));
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

        if (!context.AdditionalData.ContainsKey("startLine") || !context.AdditionalData.ContainsKey("endLine"))
        {
            return CreateErrorResult("StartLine and EndLine are required");
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
            return CreateErrorResult($"Method extraction not supported for {LanguageDetectionService.GetLanguageName(language)} files");
        }

        // Validate line numbers
        if (!int.TryParse(context.AdditionalData["startLine"]?.ToString(), out var startLine) ||
            !int.TryParse(context.AdditionalData["endLine"]?.ToString(), out var endLine))
        {
            return CreateErrorResult("StartLine and EndLine must be valid integers");
        }

        if (startLine <= 0 || endLine <= 0 || startLine > endLine)
        {
            return CreateErrorResult("StartLine and EndLine must be positive integers with StartLine <= EndLine");
        }

        return new RefactoringResult { Success = true, Message = "Validation passed" };
    }

    public async Task<RefactoringResult> ExecuteAsync(RefactoringContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ExtractMethodCommand executing for file: {FilePath}", context.FilePath);

        // Extract parameters
        var methodName = context.AdditionalData["methodName"]?.ToString() ?? throw new InvalidOperationException("MethodName is required");
        var startLine = int.Parse(context.AdditionalData["startLine"]?.ToString() ?? "0");
        var endLine = int.Parse(context.AdditionalData["endLine"]?.ToString() ?? "0");
        var previewOnly = bool.Parse(context.AdditionalData["previewOnly"]?.ToString() ?? "false");

        var validatedPath = _pathValidation.ValidateFileExists(context.FilePath);
        var language = LanguageDetectionService.DetectLanguage(validatedPath);

        _logger.LogInformation("Extracting method '{MethodName}' from {Language} file: {FilePath} (lines {StartLine}-{EndLine})", 
            methodName, language, validatedPath, startLine, endLine);

        try
        {
            return language switch
            {
                LanguageType.CSharp => await ExtractCSharpMethodAsync(context, validatedPath, methodName, startLine, endLine, previewOnly, cancellationToken),
                LanguageType.TypeScript or LanguageType.JavaScript => await ExtractTypeScriptMethodAsync(context, validatedPath, methodName, startLine, endLine, previewOnly, cancellationToken),
                _ => CreateErrorResult($"Method extraction not supported for {LanguageDetectionService.GetLanguageName(language)} files")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting method '{MethodName}' from file: {FilePath}", methodName, context.FilePath);
            return CreateErrorResult($"Failed to extract method: {ex.Message}");
        }
    }

    private async Task<RefactoringResult> ExtractCSharpMethodAsync(
        RefactoringContext context,
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("ExtractCSharpMethodAsync called with: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}", 
            filePath, methodName, startLine, endLine, previewOnly);

        var csharpContext = new RefactoringContext
        {
            FilePath = filePath,
            Language = LanguageType.CSharp
        };

        // Check for advanced parameters
        var accessModifier = context.AdditionalData.GetValueOrDefault("accessModifier")?.ToString() ?? "private";
        var isStatic = bool.Parse(context.AdditionalData.GetValueOrDefault("isStatic")?.ToString() ?? "false");
        var returnType = context.AdditionalData.GetValueOrDefault("returnType")?.ToString();

        var options = new CSharpExtractionOptions
        {
            NewMethodName = methodName,
            StartLine = startLine,
            EndLine = endLine,
            AccessModifier = accessModifier,
            IsStatic = isStatic,
            ReturnType = returnType
        };

        csharpContext.AdditionalData["extractionOptions"] = options;
        csharpContext.AdditionalData["previewOnly"] = previewOnly;

        _logger.LogDebug("Calling CSharpMethodExtractor.ExecuteAsync");
        
        var result = await _csharpMethodExtractor.ExecuteAsync(csharpContext);
        _logger.LogDebug("CSharpMethodExtractor returned: success={Success}, message={Message}", result.Success, result.Message);
        
        return result;
    }

    private async Task<RefactoringResult> ExtractTypeScriptMethodAsync(
        RefactoringContext context,
        string filePath,
        string methodName,
        int startLine,
        int endLine,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("ExtractTypeScriptMethodAsync called with: filePath={FilePath}, methodName={MethodName}, startLine={StartLine}, endLine={EndLine}, previewOnly={PreviewOnly}", 
            filePath, methodName, startLine, endLine, previewOnly);

        // Check for advanced parameters - Fixed CS8601 by ensuring accessModifier is never null
        var accessModifier = context.AdditionalData.GetValueOrDefault("accessModifier")?.ToString() ?? string.Empty;
        var isStatic = bool.Parse(context.AdditionalData.GetValueOrDefault("isStatic")?.ToString() ?? "false");
        var returnType = context.AdditionalData.GetValueOrDefault("returnType")?.ToString();

        var options = new TypeScriptExtractionOptions
        {
            NewMethodName = methodName,
            StartLine = startLine,
            EndLine = endLine,
            AccessModifier = accessModifier,
            IsStatic = isStatic,
            ReturnType = returnType,
            FunctionType = TypeScriptFunctionType.Function,
            ExportMethod = !string.IsNullOrEmpty(accessModifier) && accessModifier.ToLower() == "export"  // Fixed null reference
        };

        var result = await _typeScriptRefactoringService.ExtractMethodAsync(filePath, options, previewOnly, cancellationToken);
        
        // Convert TypeScriptExtractionResult to RefactoringResult
        return new RefactoringResult
        {
            Success = result.Success,
            Message = result.Success ? $"Successfully extracted TypeScript method '{methodName}'" : result.ErrorMessage ?? "TypeScript method extraction failed",
            Error = result.Success ? null : result.ErrorMessage,
            Changes = result.Success && !string.IsNullOrEmpty(result.ModifiedCode) ?
                [
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
            Metadata = new Dictionary<string, object>
            {
                ["ExtractedMethod"] = result.ExtractedMethod ?? "",
                ["MethodCall"] = result.MethodCall ?? "",
                ["BackupId"] = result.BackupId ?? "",
                ["MethodName"] = methodName,
                ["AccessModifier"] = accessModifier,  // Now guaranteed to be non-null
                ["IsStatic"] = isStatic,
                ["ReturnType"] = returnType ?? "",
                ["FunctionType"] = "Function"
            }
        };
    }

    private static RefactoringResult CreateErrorResult(string errorMessage)
    {
        return new RefactoringResult
        {
            Success = false,
            Error = errorMessage,
            Message = $"Extract method operation failed: {errorMessage}",
            Changes = [],
            FilesAffected = 0,
            Metadata = new Dictionary<string, object>()
        };
    }
}
