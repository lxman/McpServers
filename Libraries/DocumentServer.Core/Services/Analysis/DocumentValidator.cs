using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Analysis.Models;
using DocumentServer.Core.Services.Core;
using Microsoft.Extensions.Logging;

namespace DocumentServer.Core.Services.Analysis;

/// <summary>
/// Service for validating document integrity and structure
/// </summary>
public class DocumentValidator
{
    private readonly ILogger<DocumentValidator> _logger;
    private readonly DocumentProcessor _processor;

    /// <summary>
    /// Initializes a new instance of the DocumentValidator
    /// </summary>
    public DocumentValidator(ILogger<DocumentValidator> logger, DocumentProcessor processor)
    {
        _logger = logger;
        _processor = processor;
        _logger.LogInformation("DocumentValidator initialized");
    }

    /// <summary>
    /// Validate a document for integrity and corruption
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>Service result containing validation results</returns>
    public async Task<ServiceResult<ValidationResult>> ValidateAsync(string filePath, string? password = null)
    {
        _logger.LogInformation("Validating document: {FilePath}", filePath);

        try
        {
            var validationResult = new ValidationResult
            {
                FilePath = filePath,
                IsValid = true
            };

            // Check if file exists
            if (!File.Exists(filePath))
            {
                validationResult.IsValid = false;
                validationResult.Errors.Add("File does not exist");
                return ServiceResult<ValidationResult>.CreateSuccess(validationResult);
            }

            var fileInfo = new FileInfo(filePath);
            validationResult.FileSize = fileInfo.Length;
            validationResult.LastModified = fileInfo.LastWriteTime;

            // Check file size
            if (fileInfo.Length == 0)
            {
                validationResult.IsValid = false;
                validationResult.Errors.Add("File is empty (0 bytes)");
                return ServiceResult<ValidationResult>.CreateSuccess(validationResult);
            }

            // Check if file type is supported
            if (!_processor.IsFileTypeSupported(filePath))
            {
                validationResult.IsValid = false;
                validationResult.Errors.Add($"Unsupported file type: {Path.GetExtension(filePath)}");
                return ServiceResult<ValidationResult>.CreateSuccess(validationResult);
            }

            // Try to load the document
            var loadResult = await _processor.LoadDocumentAsync(filePath, password);
            if (!loadResult.Success)
            {
                validationResult.IsValid = false;
                validationResult.Errors.Add($"Failed to load document: {loadResult.Error}");
                
                // Check if it's a password issue
                if (loadResult.Error!.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    loadResult.Error.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
                {
                    validationResult.IsEncrypted = true;
                    validationResult.Warnings.Add("Document is encrypted and requires a password");
                }
                else
                {
                    // Not a password issue - likely corrupted
                    validationResult.IsCorrupted = true;
                }

                
                return ServiceResult<ValidationResult>.CreateSuccess(validationResult);
            }

            // Document loaded successfully
            validationResult.CanOpen = true;
            validationResult.IsEncrypted = loadResult.Data!.WasPasswordProtected;
            validationResult.DocumentType = loadResult.Data.DocumentType.ToString();

            // Try to extract content to verify integrity
            var extractResult = await _processor.ExtractTextAsync(filePath, password);
            if (!extractResult.Success)
            {
                validationResult.Warnings.Add($"Content extraction warning: {extractResult.Error}");
            }
            else
            {
                validationResult.ContentLength = extractResult.Data!.Length;
                
                if (extractResult.Data.Length == 0)
                {
                    validationResult.Warnings.Add("Document appears to be empty (no text content)");
                }
            }

            // Get metadata
            var metadataResult = 
                await _processor.ExtractMetadataAsync(filePath, password);
            if (metadataResult.Success)
            {
                validationResult.Metadata = metadataResult.Data;
            }

            _logger.LogInformation("Validation complete: {FilePath}, Valid={IsValid}, Errors={ErrorCount}",
                filePath, validationResult.IsValid, validationResult.Errors.Count);

            return ServiceResult<ValidationResult>.CreateSuccess(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document: {FilePath}", filePath);
            return ServiceResult<ValidationResult>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Quick check if a document can be opened
    /// </summary>
    /// <param name="filePath">Full path to the document</param>
    /// <param name="password">Optional password for encrypted documents</param>
    /// <returns>True if the document can be opened, otherwise false</returns>
    public async Task<bool> CanOpenAsync(string filePath, string? password = null)
    {
        try
        {
            var result = await _processor.LoadDocumentAsync(filePath, password);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }
}