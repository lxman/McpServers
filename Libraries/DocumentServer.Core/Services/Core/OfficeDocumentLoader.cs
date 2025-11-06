using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentServer.Core.Models.Common;
using Microsoft.Extensions.Logging;
using MsOfficeCrypto;
using MsOfficeCrypto.Exceptions;
using ShapeCrawler;
using DocumentType = DocumentServer.Core.Models.Common.DocumentType;

namespace DocumentServer.Core.Services.Core;

/// <summary>
/// Document loader for Microsoft Office files (Word, Excel, PowerPoint)
/// </summary>
public class OfficeDocumentLoader : IDocumentLoader
{
    private readonly ILogger<OfficeDocumentLoader> _logger;
    private readonly PasswordManager _passwordManager;

    private static readonly Dictionary<string, DocumentType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".docx", DocumentType.Word },
        { ".doc", DocumentType.Word },
        { ".xlsx", DocumentType.Excel },
        { ".xls", DocumentType.Excel },
        { ".xlsm", DocumentType.Excel },
        { ".pptx", DocumentType.PowerPoint },
        { ".ppt", DocumentType.PowerPoint }
    };

    /// <summary>
    /// Initializes a new instance of the OfficeDocumentLoader
    /// </summary>
    public OfficeDocumentLoader(ILogger<OfficeDocumentLoader> logger, PasswordManager passwordManager)
    {
        _logger = logger;
        _passwordManager = passwordManager;
        _logger.LogInformation("OfficeDocumentLoader initialized");
    }

    /// <inheritdoc />
    public DocumentType SupportedType => DocumentType.Word; // Supports multiple, this is just the primary

    /// <inheritdoc />
    public bool CanLoad(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return ExtensionMap.ContainsKey(extension);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<LoadedDocument>> LoadAsync(string filePath, string? password = null)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        if (!ExtensionMap.TryGetValue(extension, out DocumentType documentType))
        {
            return ServiceResult<LoadedDocument>.CreateFailure($"Unsupported file extension: {extension}");
        }

        _logger.LogInformation("Loading Office document: {FilePath}, Type={Type}", filePath, documentType);

        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Office file not found: {FilePath}", filePath);
                return ServiceResult<LoadedDocument>.CreateFailure("File not found");
            }

            var fileInfo = new FileInfo(filePath);
            
            // Try to get password from manager if not provided
            password ??= _passwordManager.GetPasswordForFile(filePath);

            // Load based on document type
            ServiceResult<LoadedDocument> result = documentType switch
            {
                DocumentType.Excel => await LoadExcelAsync(filePath, password, fileInfo),
                DocumentType.Word => await LoadWordAsync(filePath, password, fileInfo),
                DocumentType.PowerPoint => await LoadPowerPointAsync(filePath, password, fileInfo),
                _ => ServiceResult<LoadedDocument>.CreateFailure($"Unsupported document type: {documentType}")
            };

            if (result.Success)
            {
                _logger.LogInformation("Office document loaded successfully: {FilePath}, Type={Type}",
                    filePath, documentType);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading Office document: {FilePath}", filePath);
            return ServiceResult<LoadedDocument>.CreateFailure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<string>> ExtractTextAsync(LoadedDocument document)
    {
        _logger.LogInformation("Extracting text from Office document: {FilePath}", document.FilePath);

        try
        {
            return document.DocumentType switch
            {
                DocumentType.Excel => await ExtractExcelTextAsync(document),
                DocumentType.Word => await ExtractWordTextAsync(document),
                DocumentType.PowerPoint => await ExtractPowerPointTextAsync(document),
                _ => ServiceResult<string>.CreateFailure($"Unsupported document type: {document.DocumentType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from Office document: {FilePath}", document.FilePath);
            return ServiceResult<string>.CreateFailure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<DocumentInfo>> GetDocumentInfoAsync(string filePath, string? password = null)
    {
        _logger.LogInformation("Getting document info for Office file: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                return ServiceResult<DocumentInfo>.CreateFailure("File not found");
            }

            var fileInfo = new FileInfo(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            if (!ExtensionMap.TryGetValue(extension, out DocumentType documentType))
            {
                return ServiceResult<DocumentInfo>.CreateFailure($"Unsupported file extension: {extension}");
            }

            password ??= _passwordManager.GetPasswordForFile(filePath);

            var docInfo = new DocumentInfo
            {
                FilePath = filePath,
                DocumentType = documentType,
                SizeBytes = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                IsEncrypted = !string.IsNullOrEmpty(password)
            };

            // Try to extract metadata
            try
            {
                await using FileStream fileStream = File.OpenRead(filePath);
                await using Stream decryptedStream = await DecryptStreamAsync(fileStream, password);

                // Extract metadata based on type
                if (documentType == DocumentType.Excel)
                {
                    // For now, just basic info - can be enhanced later
                    docInfo.Metadata["Format"] = "Excel";
                }
                else if (documentType == DocumentType.Word)
                {
                    using WordprocessingDocument? wordDoc = WordprocessingDocument.Open(decryptedStream, false);
                    IPackageProperties coreProps = wordDoc.PackageProperties;
                    
                    docInfo.Author = coreProps.Creator;
                    docInfo.Title = coreProps.Title;
                    docInfo.CreatedDate = coreProps.Created;
                    docInfo.Metadata["Subject"] = coreProps.Subject ?? string.Empty;
                    docInfo.Metadata["Keywords"] = coreProps.Keywords ?? string.Empty;
                }
                else if (documentType == DocumentType.PowerPoint)
                {
                    docInfo.Metadata["Format"] = "PowerPoint";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract metadata from: {FilePath}", filePath);
            }

            return await Task.FromResult(ServiceResult<DocumentInfo>.CreateSuccess(docInfo));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document info: {FilePath}", filePath);
            return ServiceResult<DocumentInfo>.CreateFailure(ex);
        }
    }

    #region Excel Loading

    private async Task<ServiceResult<LoadedDocument>> LoadExcelAsync(string filePath, string? password, FileInfo fileInfo)
    {
        try
        {
            await using FileStream fileStream = File.OpenRead(filePath);
            await using Stream decryptedStream = await DecryptStreamAsync(fileStream, password);

            // Create a memory stream from decrypted content
            var memoryStream = new MemoryStream();
            await decryptedStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var workbook = new XLWorkbook(memoryStream);

            long estimatedMemory = fileInfo.Length * 3; // Excel files expand significantly in memory

            var loadedDocument = new LoadedDocument
            {
                FilePath = filePath,
                DocumentType = DocumentType.Excel,
                LoadedAt = DateTime.UtcNow,
                DocumentObject = workbook,
                MemorySizeBytes = estimatedMemory,
                WasPasswordProtected = !string.IsNullOrEmpty(password),
                AccessCount = 0,
                LastAccessedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Excel loaded: {FilePath}, Worksheets={Count}",
                filePath, workbook.Worksheets.Count);

            return ServiceResult<LoadedDocument>.CreateSuccess(loadedDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Excel file: {FilePath}", filePath);
            
            if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<LoadedDocument>.CreateFailure("Excel file is encrypted and requires a password");
            }
            
            return ServiceResult<LoadedDocument>.CreateFailure($"Failed to load Excel file: {ex.Message}");
        }
    }

    private async Task<ServiceResult<string>> ExtractExcelTextAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not XLWorkbook workbook)
        {
            return ServiceResult<string>.CreateFailure("Invalid document object for Excel extraction");
        }

        var textBuilder = new StringBuilder();

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            textBuilder.AppendLine($"=== Worksheet: {worksheet.Name} ===");
            textBuilder.AppendLine();

            IXLRange? usedRange = worksheet.RangeUsed();
            if (usedRange is not null)
            {
                foreach (IXLRow row in usedRange.Rows())
                {
                    List<string> values = [];
                    foreach (IXLCell cell in row.CellsUsed())
                    {
                        values.Add(cell.Value.ToString() ?? string.Empty);
                    }
                    textBuilder.AppendLine(string.Join("\t", values));
                }
            }

            textBuilder.AppendLine();
        }

        return await Task.FromResult(ServiceResult<string>.CreateSuccess(textBuilder.ToString()));
    }

    #endregion

    #region Word Loading

    private async Task<ServiceResult<LoadedDocument>> LoadWordAsync(string filePath, string? password, FileInfo fileInfo)
    {
        try
        {
            await using FileStream fileStream = File.OpenRead(filePath);
            await using Stream decryptedStream = await DecryptStreamAsync(fileStream, password);

            // Create memory stream for Word document
            var memoryStream = new MemoryStream();
            await decryptedStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            WordprocessingDocument? wordDoc = WordprocessingDocument.Open(memoryStream, false);

            long estimatedMemory = fileInfo.Length * 2;

            var loadedDocument = new LoadedDocument
            {
                FilePath = filePath,
                DocumentType = DocumentType.Word,
                LoadedAt = DateTime.UtcNow,
                DocumentObject = wordDoc,
                MemorySizeBytes = estimatedMemory,
                WasPasswordProtected = !string.IsNullOrEmpty(password),
                AccessCount = 0,
                LastAccessedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Word document loaded: {FilePath}", filePath);

            return ServiceResult<LoadedDocument>.CreateSuccess(loadedDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Word file: {FilePath}", filePath);
            
            if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<LoadedDocument>.CreateFailure("Word document is encrypted and requires a password");
            }
            
            return ServiceResult<LoadedDocument>.CreateFailure($"Failed to load Word file: {ex.Message}");
        }
    }

    private async Task<ServiceResult<string>> ExtractWordTextAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not WordprocessingDocument wordDoc)
        {
            return ServiceResult<string>.CreateFailure("Invalid document object for Word extraction");
        }

        var textBuilder = new StringBuilder();

        Body? body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is not null)
        {
            foreach (OpenXmlElement element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    string paragraphText = paragraph.InnerText;
                    textBuilder.AppendLine(paragraphText);
                }
                else if (element is Table table)
                {
                    textBuilder.AppendLine("[TABLE]");
                    // Simple table extraction
                    foreach (TableRow row in table.Elements<TableRow>())
                    {
                        List<string> cellTexts = [];
                        foreach (TableCell cell in row.Elements<TableCell>())
                        {
                            cellTexts.Add(cell.InnerText);
                        }
                        textBuilder.AppendLine(string.Join(" | ", cellTexts));
                    }
                    textBuilder.AppendLine();
                }
            }
        }

        return await Task.FromResult(ServiceResult<string>.CreateSuccess(textBuilder.ToString()));
    }

    #endregion

    #region PowerPoint Loading

    private async Task<ServiceResult<LoadedDocument>> LoadPowerPointAsync(string filePath, string? password, FileInfo fileInfo)
    {
        try
        {
            await using FileStream fileStream = File.OpenRead(filePath);
            await using Stream decryptedStream = await DecryptStreamAsync(fileStream, password);

            // Create memory stream for PowerPoint
            var memoryStream = new MemoryStream();
            await decryptedStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var presentation = new Presentation(memoryStream);

            long estimatedMemory = fileInfo.Length * 3;

            var loadedDocument = new LoadedDocument
            {
                FilePath = filePath,
                DocumentType = DocumentType.PowerPoint,
                LoadedAt = DateTime.UtcNow,
                DocumentObject = presentation,
                MemorySizeBytes = estimatedMemory,
                WasPasswordProtected = !string.IsNullOrEmpty(password),
                AccessCount = 0,
                LastAccessedAt = DateTime.UtcNow
            };

            _logger.LogDebug("PowerPoint loaded: {FilePath}, Slides={Count}",
                filePath, presentation.Slides.Count);

            return ServiceResult<LoadedDocument>.CreateSuccess(loadedDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load PowerPoint file: {FilePath}", filePath);
            
            if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<LoadedDocument>.CreateFailure("PowerPoint is encrypted and requires a password");
            }
            
            return ServiceResult<LoadedDocument>.CreateFailure($"Failed to load PowerPoint file: {ex.Message}");
        }
    }

    private async Task<ServiceResult<string>> ExtractPowerPointTextAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not Presentation presentation)
        {
            return ServiceResult<string>.CreateFailure("Invalid document object for PowerPoint extraction");
        }

        var textBuilder = new StringBuilder();

        foreach (ISlide slide in presentation.Slides)
        {
            textBuilder.AppendLine($"=== Slide {slide.Number} ===");
            textBuilder.AppendLine();

            foreach (IShape shape in slide.Shapes)
            {
                if (shape.TextBox is not null)
                {
                    textBuilder.AppendLine(shape.TextBox.Text);
                }
            }

            textBuilder.AppendLine();
        }

        return await Task.FromResult(ServiceResult<string>.CreateSuccess(textBuilder.ToString()));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Decrypt a file stream using MsOfficeCrypto
    /// </summary>
    private async Task<Stream> DecryptStreamAsync(Stream inputStream, string? password)
    {
        try
        {
            // MsOfficeCrypto handles both encrypted and unencrypted documents
            Stream decryptedStream = await OfficeDocument.DecryptAsync(inputStream, password);
            return decryptedStream;
        }
        catch (InvalidPasswordException)
        {
            _logger.LogWarning("Invalid password provided for Office document");
            throw new UnauthorizedAccessException("Invalid password provided");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Office document");
            throw;
        }
    }

    #endregion
}
