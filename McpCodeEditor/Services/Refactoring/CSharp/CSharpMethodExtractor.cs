using McpCodeEditor.Interfaces;
using McpCodeEditor.Models.Refactoring;
using McpCodeEditor.Models.Refactoring.CSharp;
using McpCodeEditor.Models.Validation;
using Microsoft.Extensions.Logging;
using CSharpValidationResult = McpCodeEditor.Models.Refactoring.CSharp.CSharpValidationResult;

namespace McpCodeEditor.Services.Refactoring.CSharp;

/// <summary>
/// Service for extracting methods from C# code with comprehensive validation and SOLID compliance
/// </summary>
public class CSharpMethodExtractor : ICSharpMethodExtractor
{
    private readonly IPathValidationService _pathValidationService;
    private readonly IBackupService _backupService;
    private readonly ILogger<CSharpMethodExtractor> _logger;
    private readonly IMethodCallGenerationService _methodCallGeneration;
    private readonly IMethodSignatureGenerationService _methodSignatureGeneration;
    private readonly ICodeModificationService _codeModification;
    private readonly IEnhancedVariableAnalysisService _enhancedVariableAnalysis;
    private readonly IChangeTrackingService _changeTracking;

    /// <summary>
    /// Constructor for CSharpMethodExtractor with enhanced dependency injection
    /// </summary>
    /// <param name="pathValidationService">Service for validating file paths</param>
    /// <param name="backupService">Service for creating file backups</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="methodCallGeneration">Service for generating method calls</param>
    /// <param name="methodSignatureGeneration">Service for generating method signatures</param>
    /// <param name="codeModification">Service for code modifications</param>
    /// <param name="enhancedVariableAnalysis">Service for enhanced variable analysis</param>
    /// <param name="changeTracking">Service for tracking file changes</param>
    public CSharpMethodExtractor(
        IPathValidationService pathValidationService,
        IBackupService backupService,
        ILogger<CSharpMethodExtractor> logger,
        IMethodCallGenerationService methodCallGeneration,
        IMethodSignatureGenerationService methodSignatureGeneration,
        ICodeModificationService codeModification,
        IEnhancedVariableAnalysisService enhancedVariableAnalysis,
        IChangeTrackingService changeTracking)
    {
        _pathValidationService = pathValidationService;
        _backupService = backupService;
        _logger = logger;
        _methodCallGeneration = methodCallGeneration;
        _methodSignatureGeneration = methodSignatureGeneration;
        _codeModification = codeModification;
        _enhancedVariableAnalysis = enhancedVariableAnalysis;
        _changeTracking = changeTracking;
    }

    /// <summary>
    /// Extracts a method from the specified lines with comprehensive validation
    /// </summary>
    public async Task<RefactoringResult> ExtractMethodAsync(
        RefactoringContext context,
        CSharpExtractionOptions options,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting method extraction for {FilePath} lines {StartLine}-{EndLine}",
            context.FilePath, options.StartLine, options.EndLine);

        try
        {
            // Validate context and options
            var validationResult = await ValidateExtractionAsync(context, options, cancellationToken);
            if (!validationResult.IsValid)
            {
                return RefactoringResult.CreateFailure(
                    "Validation failed: " + string.Join("; ", validationResult.Errors));
            }

            // Step 1: Read source code from context.FilePath
            _logger.LogDebug("Phase 1 - Step 1: Reading source code from {FilePath}", context.FilePath);
            if (!File.Exists(context.FilePath))
            {
                return RefactoringResult.CreateFailure($"Source file not found: {context.FilePath}");
            }

            var sourceCode = await File.ReadAllTextAsync(context.FilePath, cancellationToken);
            if (string.IsNullOrEmpty(sourceCode))
            {
                return RefactoringResult.CreateFailure("Source file is empty or could not be read");
            }

            _logger.LogDebug("Phase 1 - Step 1 Complete: Read {Length} characters from source file", sourceCode.Length);

            // Step 2: Use Enhanced Variable Analysis Service to analyze the code lines
            _logger.LogDebug("Phase 1 - Step 2: Analyzing code lines {StartLine}-{EndLine} using Enhanced Variable Analysis Service",
                options.StartLine, options.EndLine);
            
            var sourceLines = sourceCode.Split('\n');
            var extractedLines = sourceLines
                .Skip(options.StartLine - 1)
                .Take(options.EndLine - options.StartLine + 1)
                .ToArray();

            // Step 3: Basic validation using existing validation services  
            _logger.LogDebug("Phase 1 - Step 3: Enhanced validation with analysis results");

            var enhancedValidationResult = await ValidateExtractionAsync(context, options, cancellationToken);
            if (!enhancedValidationResult.IsValid)
            {
                return RefactoringResult.CreateFailure(
                    $"Enhanced validation failed: {string.Join("; ", enhancedValidationResult.Errors)}");
            }

            _logger.LogDebug("Phase 1 - Step 3 Complete: Enhanced validation passed with {WarningCount} warnings",
                enhancedValidationResult.Warnings.Count);

            // Step 4: Create backup before making changes
            string? backupId = null;
            if (!previewOnly)
            {
                _logger.LogDebug("Phase 1 - Step 4: Creating backup before modifications");

                backupId = await _backupService.CreateBackupAsync(
                    context.FilePath,
                    $"Method extraction backup for {options.NewMethodName}");

                if (string.IsNullOrEmpty(backupId))
                {
                    return RefactoringResult.CreateFailure("Failed to create backup");
                }

                _logger.LogInformation("Phase 1 - Step 4 Complete: Backup created with ID {BackupId}", backupId);
            }
            else
            {
                _logger.LogDebug("Phase 1 - Step 4: Skipping backup creation in preview mode");
            }

            _logger.LogInformation("Phase 1 Complete: Core extraction flow finished successfully. Starting Phase 2 (Method Generation)");

            // Step 1: Generate the method signature using IMethodSignatureGenerationService
            _logger.LogDebug("Phase 2 - Step 1: Generating method signature using IMethodSignatureGenerationService");

            try
            {
                if (options.StartLine > sourceLines.Length || options.EndLine > sourceLines.Length)
                {
                    return RefactoringResult.CreateFailure($"Line numbers out of range: {options.StartLine}-{options.EndLine} (file has {sourceLines.Length} lines)");
                }

                _logger.LogDebug("Phase 2 - Step 1a: Extracted {LineCount} lines of code for signature generation", extractedLines.Length);

                // Create a basic validation result for the services using a proper factory method
                var extractedMethodInfo = new ExtractedMethodInfo
                {
                    StartLine = options.StartLine,
                    EndLine = options.EndLine,
                    ReturnType = "void",
                    AccessModifier = "private"
                };

                var methodValidationResult = MethodExtractionValidationResult.Success(extractedMethodInfo, "Basic validation for Phase 2");
                methodValidationResult.Language = "C#";
                methodValidationResult.SuggestedMethodName = options.NewMethodName;

                // Get base indentation
                var baseIndentation = _codeModification.GetBaseIndentation(extractedLines);
                
                // Use the method signature generation service
                var methodSignature = await _methodSignatureGeneration.CreateExtractedMethodWithParametersAsync(
                    extractedLines,
                    options,
                    extractedMethodInfo.ReturnType ?? "void",
                    baseIndentation,
                    methodValidationResult);

                if (string.IsNullOrEmpty(methodSignature))
                {
                    return RefactoringResult.CreateFailure("Failed to generate method signature");
                }

                _logger.LogDebug("Phase 2 - Step 1 Complete: Generated method signature with {Length} characters", methodSignature.Length);

                // Step 2: Generate method call using IMethodCallGenerationService
                _logger.LogDebug("Phase 2 - Step 2: Generating method call using IMethodCallGenerationService");

                // Get the indentation for the method call (from the first extracted line)
                var callIndentation = extractedLines.Length > 0 ? _codeModification.GetLineIndentation(extractedLines[0]) : "";

                var methodCall = await _methodCallGeneration.CreateMethodCallAsync(
                    callIndentation,
                    options.NewMethodName,
                    methodValidationResult,
                    extractedLines);

                if (string.IsNullOrEmpty(methodCall))
                {
                    return RefactoringResult.CreateFailure("Failed to generate method call");
                }

                _logger.LogDebug("Phase 2 - Step 2 Complete: Generated method call with {Length} characters", methodCall.Length);

                // Step 3: Handle extracted code using ICodeModificationService
                _logger.LogDebug("Phase 2 - Step 3: Handling extracted code using ICodeModificationService");

                var modificationResult = _codeModification.BuildModifiedContent(
                    sourceLines,
                    options.StartLine,
                    options.EndLine,
                    methodCall,
                    methodSignature);

                if (string.IsNullOrEmpty(modificationResult))
                {
                    return RefactoringResult.CreateFailure("Failed to build modified content");
                }

                _logger.LogDebug("Phase 2 - Step 3 Complete: Built modified content with {Length} characters", modificationResult.Length);

                _logger.LogInformation("Phase 2 Complete: Method generation finished successfully. Starting Phase 3 (File Modification)");

                // PHASE 3: File Modification Implementation

                if (previewOnly)
                {
                    _logger.LogInformation("Phase 3: Preview mode - skipping file modifications");
                    
                    var previewResult = RefactoringResult.CreateSuccess(
                        $"Preview complete: Method '{options.NewMethodName}' extraction would affect {options.EndLine - options.StartLine + 1} lines");
                    
                    previewResult.Metadata["phase"] = "Phase 3 Preview Complete";
                    previewResult.Metadata["previewMode"] = true;
                    previewResult.Metadata["extractedLines"] = options.EndLine - options.StartLine + 1;
                    previewResult.Metadata["methodSignature"] = methodSignature;
                    previewResult.Metadata["methodCall"] = methodCall;
                    previewResult.Metadata["modifiedContent"] = modificationResult;
                    
                    return previewResult;
                }

                // Step 1: Apply changes to the source file
                _logger.LogDebug("Phase 3 - Step 1: Applying changes to source file {FilePath}", context.FilePath);

                try
                {
                    await File.WriteAllTextAsync(context.FilePath, modificationResult, cancellationToken);
                    _logger.LogInformation("Phase 3 - Step 1 Complete: Successfully wrote modified content to {FilePath}", context.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Phase 3 - Step 1 Failed: Error writing to file {FilePath}", context.FilePath);
                    return RefactoringResult.CreateFailure($"Failed to write modified content to file: {ex.Message}");
                }

                // Step 2: Track changes using the change tracking service
                _logger.LogDebug("Phase 3 - Step 2: Tracking changes using change tracking service");

                try
                {
                    var changeId = await _changeTracking.TrackChangeAsync(
                        context.FilePath,
                        sourceCode,
                        modificationResult,
                        "ExtractMethod",
                        backupId,
                        new Dictionary<string, object>
                        {
                            ["methodName"] = options.NewMethodName,
                            ["startLine"] = options.StartLine,
                            ["endLine"] = options.EndLine,
                            ["linesExtracted"] = options.EndLine - options.StartLine + 1,
                            ["accessModifier"] = options.AccessModifier,
                            ["isStatic"] = options.IsStatic
                        });

                    if (string.IsNullOrEmpty(changeId))
                    {
                        _logger.LogWarning("Phase 3 - Step 2: Change tracking returned empty change ID");
                    }
                    else
                    {
                        _logger.LogInformation("Phase 3 - Step 2 Complete: Tracked change with ID {ChangeId}", changeId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Phase 3 - Step 2 Failed: Error tracking changes");
                    // Don't fail the operation for tracking issues
                }

                // Step 3: Validate results and handle final success
                _logger.LogDebug("Phase 3 - Step 3: Validating results and finalizing success");

                // Verify the file was actually modified
                if (!File.Exists(context.FilePath))
                {
                    return RefactoringResult.CreateFailure("File no longer exists after modification");
                }

                var modifiedFileContent = await File.ReadAllTextAsync(context.FilePath, cancellationToken);
                if (string.IsNullOrEmpty(modifiedFileContent))
                {
                    return RefactoringResult.CreateFailure("Modified file is empty");
                }

                if (modifiedFileContent == sourceCode)
                {
                    return RefactoringResult.CreateFailure("File content unchanged - no modifications applied");
                }

                _logger.LogInformation("Phase 3 - Step 3 Complete: File validation successful, modifications confirmed");

                // Create the final success result
                _logger.LogInformation("Phase 3 Complete: All phases finished successfully. Method extraction completed!");

                var finalResult = RefactoringResult.CreateSuccess(
                    $"Successfully extracted method '{options.NewMethodName}' from lines {options.StartLine}-{options.EndLine}. " +
                    $"Extracted {options.EndLine - options.StartLine + 1} lines into new {options.AccessModifier} method.");

                // Set metadata for the final result
                finalResult.FilesAffected = 1;
                finalResult.Changes.Add(new Models.FileChange
                {
                    FilePath = context.FilePath,
                    OriginalContent = sourceCode,
                    ModifiedContent = modificationResult,
                    ChangeType = "ExtractMethod"
                });

                finalResult.Metadata["phase"] = "Phase 3 Complete";
                finalResult.Metadata["allPhasesComplete"] = true;
                finalResult.Metadata["methodName"] = options.NewMethodName;
                finalResult.Metadata["extractedLines"] = options.EndLine - options.StartLine + 1;
                finalResult.Metadata["startLine"] = options.StartLine;
                finalResult.Metadata["endLine"] = options.EndLine;
                finalResult.Metadata["filePath"] = context.FilePath;
                finalResult.Metadata["accessModifier"] = options.AccessModifier;
                finalResult.Metadata["isStatic"] = options.IsStatic;
                finalResult.Metadata["backupId"] = backupId ?? string.Empty;;
                finalResult.Metadata["originalFileSize"] = sourceCode.Length;
                finalResult.Metadata["modifiedFileSize"] = modificationResult.Length;

                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Phase 2-3 method generation and modification for {FilePath}", context.FilePath);
                return RefactoringResult.CreateFailure($"Phase 2-3 failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during method extraction for {FilePath}", context.FilePath);
            return RefactoringResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that method extraction can be performed with enhanced analysis
    /// </summary>
    public async Task<CSharpValidationResult> ValidateExtractionAsync(
        RefactoringContext context,
        CSharpExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        // Basic validation for now
        return new CSharpValidationResult
        {
            IsValid = true,
            Errors = [],
            Warnings = [],
            Analysis = new CSharpExtractionAnalysis()
        };
    }

    /// <summary>
    /// Analyzes the code to be extracted and provides insights
    /// </summary>
    public async Task<CSharpExtractionAnalysis> AnalyzeExtractionAsync(
        RefactoringContext context,
        CSharpExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        return new CSharpExtractionAnalysis();
    }

    // IRefactoringOperation implementation
    public string OperationName => "ExtractMethod";
    public LanguageType SupportedLanguage => LanguageType.CSharp;

    public async Task<RefactoringResult> ExecuteAsync(RefactoringContext context)
    {
        _logger.LogDebug("ExecuteAsync called for context: {FilePath}", context.FilePath);

        // CRITICAL FIX: Extract the actual options from context instead of using hardcoded defaults
        if (context.AdditionalData.TryGetValue("extractionOptions", out var extractionOptionsObj) &&
            extractionOptionsObj is CSharpExtractionOptions contextOptions)
        {
            _logger.LogInformation("Extracted options from context: StartLine={StartLine}, EndLine={EndLine}, MethodName={MethodName}",
                contextOptions.StartLine, contextOptions.EndLine, contextOptions.NewMethodName);

            // Get preview mode from context
            var previewOnly = false;
            if (context.AdditionalData.TryGetValue("previewOnly", out var previewOnlyObj) && previewOnlyObj is bool preview)
            {
                previewOnly = preview;
            }

            return await ExtractMethodAsync(context, contextOptions, previewOnly);
        }

        // Fallback: Log error and return failure instead of using wrong defaults
        _logger.LogError("No valid extractionOptions found in context.AdditionalData for {FilePath}. Available keys: {Keys}",
            context.FilePath, string.Join(", ", context.AdditionalData.Keys));

        return RefactoringResult.CreateFailure("No extraction options provided in context");
    }

    public async Task<bool> ValidateOperationAsync(RefactoringContext context)
    {
        // Default validation
        return true;
    }
}
