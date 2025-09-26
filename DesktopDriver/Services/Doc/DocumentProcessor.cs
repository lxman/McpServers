using System.Text;
using DesktopDriver.Services.Doc.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using DocType = DesktopDriver.Services.Doc.Models.DocumentType;

namespace DesktopDriver.Services.Doc;

public class DocumentProcessor
{
    private readonly ILogger<DocumentProcessor> _logger;
    private readonly PasswordManager _passwordManager;

    public DocumentProcessor(ILogger<DocumentProcessor> logger, PasswordManager passwordManager)
    {
        _logger = logger;
        _passwordManager = passwordManager;
    }

    public async Task<DocumentContent> ExtractContent(string filePath, string? password = null)
    {
        try
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            DocType documentType = GetDocumentType(extension);
            
            // Use provided password or try to find one
            password ??= _passwordManager.GetPasswordForFile(filePath);

            _logger.LogDebug("Processing document: {FilePath} (Type: {DocumentType})", filePath, documentType);

            var content = new DocumentContent
            {
                FilePath = filePath,
                DocumentType = documentType,
                Metadata = ExtractMetadata(filePath),
                RequiredPassword = !string.IsNullOrEmpty(password)
            };

            // Extract content based on document type
            switch (documentType)
            {
                case DocType.Word:
                    ExtractWordContent(content, password);
                    break;
                case DocType.Excel:
                    ExtractExcelContent(content, password);
                    break;
                case DocType.Pdf:
                    ExtractPdfContent(content, password);
                    break;
                case DocType.PowerPoint:
                    ExtractPowerPointContent(content, password);
                    break;
                case DocType.PlainText:
                case DocType.Markdown:
                    await ExtractTextContent(content);
                    break;
                case DocType.Csv:
                    await ExtractCsvContent(content);
                    break;
                default:
                    await ExtractGenericContent(content);
                    break;
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract content from: {FilePath}", filePath);
            throw;
        }
    }

    private static DocType GetDocumentType(string extension)
    {
        return extension switch
        {
            ".docx" or ".doc" => DocType.Word,
            ".xlsx" or ".xls" => DocType.Excel,
            ".pptx" or ".ppt" => DocType.PowerPoint,
            ".pdf" => DocType.Pdf,
            ".txt" => DocType.PlainText,
            ".md" or ".markdown" => DocType.Markdown,
            ".html" or ".htm" => DocType.Html,
            ".rtf" => DocType.Rtf,
            ".csv" => DocType.Csv,
            _ => DocType.Unknown
        };
    }

    private static DocumentMetadata ExtractMetadata(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        
        return new DocumentMetadata
        {
            FileName = fileInfo.Name,
            FileSizeBytes = fileInfo.Length,
            CreatedDate = fileInfo.CreationTime,
            ModifiedDate = fileInfo.LastWriteTime,
            AccessedDate = fileInfo.LastAccessTime
        };
    }

    private void ExtractWordContent(DocumentContent content, string? password)
    {
        try
        {
            // For password-protected Word documents, we'll use NPOI instead of OpenXML
            if (!string.IsNullOrEmpty(password))
            {
                ExtractWordContentWithNPOI(content, password);
                return;
            }

            // Try OpenXML first for non-password protected docs
            using WordprocessingDocument document = WordprocessingDocument.Open(content.FilePath, false);
            
            // Extract basic text
            Body? body = document.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                content.PlainText = body.InnerText;

                // Extract structured content
                ExtractWordStructure(content, body);
            }

            // Extract document properties
            if (document.PackageProperties != null)
            {
                content.Metadata.Author = document.PackageProperties.Creator ?? "";
                content.Metadata.Subject = document.PackageProperties.Subject ?? "";
                content.Metadata.Keywords = document.PackageProperties.Keywords ?? "";
                content.Title = document.PackageProperties.Title ?? Path.GetFileNameWithoutExtension(content.FilePath);
            }
        }
        catch (Exception ex) when (ex.Message.Contains("password") || ex.Message.Contains("encrypted"))
        {
            _logger.LogWarning("Password required for Word document: {FilePath}", content.FilePath);
            throw new UnauthorizedAccessException($"Password required for document: {content.FilePath}");
        }
    }

    private void ExtractWordContentWithNPOI(DocumentContent content, string password)
    {
        try
        {
            // NPOI doesn't have great Word password support, so we'll provide basic info
            content.PlainText = $"Password-protected Word document: {Path.GetFileName(content.FilePath)}";
            content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
            
            _logger.LogWarning("NPOI Word password support is limited. Consider manual extraction for: {FilePath}", content.FilePath);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException($"Failed to open password-protected Word document: {ex.Message}");
        }
    }

    private static void ExtractWordStructure(DocumentContent content, Body body)
    {
        var sections = new List<DocumentSection>();
        var currentSection = new DocumentSection();
        var contentBuilder = new StringBuilder();

        foreach (OpenXmlElement element in body.Elements())
        {
            if (element is Paragraph para)
            {
                string text = para.InnerText;
                
                // Check if this is a heading
                int heading = GetHeadingLevel(para);
                if (heading > 0 && !string.IsNullOrWhiteSpace(text))
                {
                    // Save previous section
                    if (!string.IsNullOrWhiteSpace(currentSection.Content))
                    {
                        sections.Add(currentSection);
                    }

                    // Start new section
                    currentSection = new DocumentSection
                    {
                        Title = text.Trim(),
                        Level = heading
                    };
                    contentBuilder.Clear();
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    contentBuilder.AppendLine(text);
                }
            }
        }

        // Add final section
        currentSection.Content = contentBuilder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(currentSection.Content) || !string.IsNullOrWhiteSpace(currentSection.Title))
        {
            sections.Add(currentSection);
        }

        content.Sections = sections;
    }

    private static int GetHeadingLevel(Paragraph paragraph)
    {
        string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId != null)
        {
            if (styleId.StartsWith("Heading"))
            {
                if (int.TryParse(styleId.Replace("Heading", ""), out int level))
                {
                    return level;
                }
            }
        }
        return 0;
    }

    private void ExtractExcelContent(DocumentContent content, string? password)
    {
        try
        {
            // Try NPOI first (better password support)
            using IWorkbook? workbook = WorkbookFactory.Create(content.FilePath, password);
            
            var textBuilder = new StringBuilder();
            var structuredData = new Dictionary<string, object>();

            for (var i = 0; i < workbook.NumberOfSheets; i++)
            {
                ISheet? sheet = workbook.GetSheetAt(i);
                string? sheetName = sheet.SheetName;
                
                textBuilder.AppendLine($"=== Sheet: {sheetName} ===");
                
                var sheetData = new List<List<string>>();
                
                foreach (IRow row in sheet)
                {
                    var rowData = new List<string>();
                    foreach (ICell? cell in row)
                    {
                        string cellValue = cell?.ToString() ?? "";
                        rowData.Add(cellValue);
                        if (!string.IsNullOrWhiteSpace(cellValue))
                        {
                            textBuilder.Append(cellValue).Append("\t");
                        }
                    }
                    
                    if (rowData.Any(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        sheetData.Add(rowData);
                        textBuilder.AppendLine();
                    }
                }
                
                structuredData[sheetName] = sheetData;
            }

            content.PlainText = textBuilder.ToString();
            content.StructuredData = structuredData;
            content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
        }
        catch (Exception ex) when (ex.Message.Contains("password") || ex.Message.Contains("encrypted"))
        {
            _logger.LogWarning("Password required for Excel document: {FilePath}", content.FilePath);
            throw new UnauthorizedAccessException($"Password required for document: {content.FilePath}");
        }
    }

    private void ExtractPdfContent(DocumentContent content, string? password)
    {
        try
        {
            var readerProperties = new ReaderProperties();
            if (!string.IsNullOrEmpty(password))
            {
                readerProperties.SetPassword(Encoding.UTF8.GetBytes(password));
            }

            using var pdfReader = new PdfReader(content.FilePath, readerProperties);
            using var pdfDocument = new PdfDocument(pdfReader);

            var textBuilder = new StringBuilder();
            int pageCount = pdfDocument.GetNumberOfPages();
            
            for (var i = 1; i <= pageCount; i++)
            {
                string? pageText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(i));
                textBuilder.AppendLine(pageText);
                
                if (i == 1 && !string.IsNullOrWhiteSpace(pageText))
                {
                    // Use first line of first page as title if no title set
                    string? firstLine = pageText.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                    if (!string.IsNullOrWhiteSpace(firstLine) && firstLine.Length < 100)
                    {
                        content.Title = firstLine.Trim();
                    }
                }
            }

            content.PlainText = textBuilder.ToString();
            content.Metadata.PageCount = pageCount;
            
            if (string.IsNullOrWhiteSpace(content.Title))
            {
                content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
            }
        }
        catch (Exception ex) when (ex.Message.Contains("password") || ex.Message.Contains("encrypted") || ex.Message.Contains("Bad user password"))
        {
            _logger.LogWarning("Password required for PDF document: {FilePath}", content.FilePath);
            throw new UnauthorizedAccessException($"Password required for document: {content.FilePath}");
        }
    }

    private void ExtractPowerPointContent(DocumentContent content, string? password)
    {
        try
        {
            // For password-protected PowerPoint, we'll provide basic info
            if (!string.IsNullOrEmpty(password))
            {
                content.PlainText = $"Password-protected PowerPoint document: {Path.GetFileName(content.FilePath)}";
                content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
                _logger.LogWarning("PowerPoint password protection not fully supported: {FilePath}", content.FilePath);
                return;
            }

            using PresentationDocument presentation = PresentationDocument.Open(content.FilePath, false);
            
            var textBuilder = new StringBuilder();
            IEnumerable<SlideId>? slides = presentation.PresentationPart?.Presentation?.SlideIdList?.Elements<SlideId>();
            
            if (slides != null)
            {
                var slideNumber = 1;
                foreach (SlideId slide in slides)
                {
                    var slidePart = presentation.PresentationPart?.GetPartById(slide.RelationshipId!) as SlidePart;
                    if (slidePart != null)
                    {
                        textBuilder.AppendLine($"=== Slide {slideNumber} ===");
                        textBuilder.AppendLine(slidePart.Slide.InnerText);
                        slideNumber++;
                    }
                }
            }

            content.PlainText = textBuilder.ToString();
            content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
        }
        catch (Exception ex) when (ex.Message.Contains("password") || ex.Message.Contains("encrypted"))
        {
            _logger.LogWarning("Password required for PowerPoint document: {FilePath}", content.FilePath);
            throw new UnauthorizedAccessException($"Password required for document: {content.FilePath}");
        }
    }

    private static async Task ExtractTextContent(DocumentContent content)
    {
        content.PlainText = await File.ReadAllTextAsync(content.FilePath);
        content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
        content.Metadata.CharacterCount = content.PlainText.Length;
        content.Metadata.WordCount = content.PlainText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static async Task ExtractCsvContent(DocumentContent content)
    {
        string[] lines = await File.ReadAllLinesAsync(content.FilePath);
        content.PlainText = string.Join(Environment.NewLine, lines);
        content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
        
        // Try to parse as structured data
        if (lines.Length > 0)
        {
            var csvData = new List<List<string>>();
            foreach (string line in lines)
            {
                List<string> values = line.Split(',').Select(v => v.Trim('"').Trim()).ToList();
                csvData.Add(values);
            }
            content.StructuredData["csv_data"] = csvData;
        }
    }

    private static async Task ExtractGenericContent(DocumentContent content)
    {
        try
        {
            // Attempt to read as text
            content.PlainText = await File.ReadAllTextAsync(content.FilePath);
            content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
        }
        catch
        {
            // If text reading fails, just set basic info
            content.PlainText = $"Binary file: {Path.GetFileName(content.FilePath)}";
            content.Title = Path.GetFileNameWithoutExtension(content.FilePath);
        }
    }
}