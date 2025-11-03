using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentServer.Models.Common;
using ShapeCrawler;
using DocumentType = DocumentServer.Models.Common.DocumentType;

namespace DocumentServer.Services.Core;

/// <summary>
/// Content extractor for Microsoft Office documents (Word, Excel, PowerPoint)
/// </summary>
public class OfficeContentExtractor : IContentExtractor
{
    private readonly ILogger<OfficeContentExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the OfficeContentExtractor
    /// </summary>
    public OfficeContentExtractor(ILogger<OfficeContentExtractor> logger)
    {
        _logger = logger;
        _logger.LogInformation("OfficeContentExtractor initialized");
    }

    /// <inheritdoc />
    public DocumentType SupportedType => DocumentType.Word; // Supports all Office types

    /// <inheritdoc />
    public async Task<ServiceResult<string>> ExtractTextAsync(LoadedDocument document, int? startPage = null, int? endPage = null, int? maxPages = null)
    {
        _logger.LogInformation("Extracting text from Office document: {FilePath}, Type={Type}",
            document.FilePath, document.DocumentType);

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
    public async Task<ServiceResult<Dictionary<string, string>>> ExtractMetadataAsync(LoadedDocument document)
    {
        _logger.LogInformation("Extracting metadata from Office document: {FilePath}, Type={Type}",
            document.FilePath, document.DocumentType);

        try
        {
            return document.DocumentType switch
            {
                DocumentType.Excel => await ExtractExcelMetadataAsync(document),
                DocumentType.Word => await ExtractWordMetadataAsync(document),
                DocumentType.PowerPoint => await ExtractPowerPointMetadataAsync(document),
                _ => ServiceResult<Dictionary<string, string>>.CreateFailure($"Unsupported document type: {document.DocumentType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from Office document: {FilePath}", document.FilePath);
            return ServiceResult<Dictionary<string, string>>.CreateFailure(ex);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<object>> ExtractStructuredContentAsync(LoadedDocument document)
    {
        _logger.LogInformation("Extracting structured content from Office document: {FilePath}, Type={Type}",
            document.FilePath, document.DocumentType);

        try
        {
            return document.DocumentType switch
            {
                DocumentType.Excel => await ExtractExcelStructuredContentAsync(document),
                DocumentType.Word => await ExtractWordStructuredContentAsync(document),
                DocumentType.PowerPoint => await ExtractPowerPointStructuredContentAsync(document),
                _ => ServiceResult<object>.CreateFailure($"Unsupported document type: {document.DocumentType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting structured content from Office document: {FilePath}", document.FilePath);
            return ServiceResult<object>.CreateFailure(ex);
        }
    }

    #region Excel Extraction

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
                    if (values.Count > 0)
                    {
                        textBuilder.AppendLine(string.Join("\t", values));
                    }
                }
            }

            textBuilder.AppendLine();
        }

        _logger.LogInformation("Excel text extraction complete: {FilePath}, Worksheets={Count}",
            document.FilePath, workbook.Worksheets.Count);

        return await Task.FromResult(ServiceResult<string>.CreateSuccess(textBuilder.ToString()));
    }

    private async Task<ServiceResult<Dictionary<string, string>>> ExtractExcelMetadataAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not XLWorkbook workbook)
        {
            return ServiceResult<Dictionary<string, string>>.CreateFailure("Invalid document object");
        }

        var metadata = new Dictionary<string, string>
        {
            ["WorksheetCount"] = workbook.Worksheets.Count.ToString(),
            ["Author"] = workbook.Author ?? string.Empty,
            ["Title"] = "Excel Workbook"
        };

        // Calculate total cells with data
        var totalCells = 0;
        var totalRows = 0;
        var worksheetNames = new List<string>();

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            worksheetNames.Add(worksheet.Name);
            IXLRange? usedRange = worksheet.RangeUsed();
            if (usedRange is not null)
            {
                totalCells += usedRange.CellsUsed().Count();
                totalRows += usedRange.RowCount();
            }
        }

        metadata["TotalCellsWithData"] = totalCells.ToString();
        metadata["TotalRows"] = totalRows.ToString();
        metadata["WorksheetNames"] = string.Join(", ", worksheetNames);

        return await Task.FromResult(ServiceResult<Dictionary<string, string>>.CreateSuccess(metadata));
    }

    private async Task<ServiceResult<object>> ExtractExcelStructuredContentAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not XLWorkbook workbook)
        {
            return ServiceResult<object>.CreateFailure("Invalid document object");
        }

        var worksheets = new List<Dictionary<string, object>>();

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            IXLRange? usedRange = worksheet.RangeUsed();
            
            var worksheetData = new Dictionary<string, object>
            {
                ["Name"] = worksheet.Name,
                ["Index"] = worksheet.Position,
                ["RowCount"] = usedRange?.RowCount() ?? 0,
                ["ColumnCount"] = usedRange?.ColumnCount() ?? 0,
                ["IsVisible"] = worksheet.Visibility == XLWorksheetVisibility.Visible
            };

            // Extract sample data (first 10 rows)
            if (usedRange is not null)
            {
                var sampleData = new List<List<string>>();
                var rowsExtracted = 0;
                
                foreach (IXLRow row in usedRange.Rows().Take(10))
                {
                    List<string> rowData = row.CellsUsed().Select(c => c.Value.ToString() ?? string.Empty).ToList();
                    sampleData.Add(rowData);
                    rowsExtracted++;
                }
                
                worksheetData["SampleData"] = sampleData;
                worksheetData["SampleRowCount"] = rowsExtracted;
            }

            worksheets.Add(worksheetData);
        }

        var structuredContent = new Dictionary<string, object>
        {
            ["WorkbookInfo"] = new
            {
                WorksheetCount = workbook.Worksheets.Count, workbook.Author
            },
            ["Worksheets"] = worksheets
        };

        return await Task.FromResult(ServiceResult<object>.CreateSuccess(structuredContent));
    }

    #endregion

    #region Word Extraction

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
                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        textBuilder.AppendLine(paragraphText);
                    }
                }
                else if (element is Table table)
                {
                    textBuilder.AppendLine("[TABLE]");
                    foreach (TableRow row in table.Elements<TableRow>())
                    {
                        List<string> cellTexts = [];
                        cellTexts.AddRange(row.Elements<TableCell>().Select(cell => cell.InnerText.Trim()));
                        textBuilder.AppendLine(string.Join(" | ", cellTexts));
                    }
                    textBuilder.AppendLine();
                }
            }
        }

        _logger.LogInformation("Word text extraction complete: {FilePath}", document.FilePath);

        return await Task.FromResult(ServiceResult<string>.CreateSuccess(textBuilder.ToString()));
    }

    private async Task<ServiceResult<Dictionary<string, string>>> ExtractWordMetadataAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not WordprocessingDocument wordDoc)
        {
            return ServiceResult<Dictionary<string, string>>.CreateFailure("Invalid document object");
        }

        var metadata = new Dictionary<string, string>();
        IPackageProperties coreProps = wordDoc.PackageProperties;

        metadata["Title"] = coreProps.Title ?? string.Empty;
        metadata["Author"] = coreProps.Creator ?? string.Empty;
        metadata["Subject"] = coreProps.Subject ?? string.Empty;
        metadata["Keywords"] = coreProps.Keywords ?? string.Empty;
        metadata["LastModifiedBy"] = coreProps.LastModifiedBy ?? string.Empty;
        
        if (coreProps.Created.HasValue)
        {
            metadata["Created"] = coreProps.Created.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }
        
        if (coreProps.Modified.HasValue)
        {
            metadata["Modified"] = coreProps.Modified.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // Count elements
        Body? body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return await Task.FromResult(ServiceResult<Dictionary<string, string>>.CreateSuccess(metadata));
        metadata["ParagraphCount"] = body.Elements<Paragraph>().Count().ToString();
        metadata["TableCount"] = body.Elements<Table>().Count().ToString();

        return await Task.FromResult(ServiceResult<Dictionary<string, string>>.CreateSuccess(metadata));
    }

    private async Task<ServiceResult<object>> ExtractWordStructuredContentAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not WordprocessingDocument wordDoc)
        {
            return ServiceResult<object>.CreateFailure("Invalid document object");
        }

        var paragraphs = new List<Dictionary<string, object>>();
        var tables = new List<Dictionary<string, object>>();

        Body? body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is not null)
        {
            var paragraphIndex = 0;
            foreach (Paragraph paragraph in body.Elements<Paragraph>())
            {
                var paragraphData = new Dictionary<string, object>
                {
                    ["Index"] = paragraphIndex++,
                    ["Text"] = paragraph.InnerText,
                    ["IsEmpty"] = string.IsNullOrWhiteSpace(paragraph.InnerText)
                };
                
                // Check if it's a heading
                string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                if (styleId is not null && styleId.StartsWith("Heading"))
                {
                    paragraphData["IsHeading"] = true;
                    paragraphData["HeadingLevel"] = styleId;
                }
                
                paragraphs.Add(paragraphData);
            }

            var tableIndex = 0;
            tables.AddRange(body.Elements<Table>().Select(table => new Dictionary<string, object> { ["Index"] = tableIndex++, ["RowCount"] = table.Elements<TableRow>().Count(), ["ColumnCount"] = table.Elements<TableRow>().FirstOrDefault()?.Elements<TableCell>().Count() ?? 0 }));
        }

        var structuredContent = new Dictionary<string, object>
        {
            ["DocumentInfo"] = new
            {
                ParagraphCount = paragraphs.Count,
                TableCount = tables.Count
            },
            ["Paragraphs"] = paragraphs.Take(50).ToList(), // First 50 paragraphs
            ["Tables"] = tables
        };

        return await Task.FromResult(ServiceResult<object>.CreateSuccess(structuredContent));
    }

    #endregion

    #region PowerPoint Extraction

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
                    string text = shape.TextBox.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textBuilder.AppendLine(text);
                    }
                }
            }

            textBuilder.AppendLine();
        }

        _logger.LogInformation("PowerPoint text extraction complete: {FilePath}, Slides={Count}",
            document.FilePath, presentation.Slides.Count);

        return await Task.FromResult(ServiceResult<string>.CreateSuccess(textBuilder.ToString()));
    }

    private async Task<ServiceResult<Dictionary<string, string>>> ExtractPowerPointMetadataAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not Presentation presentation)
        {
            return ServiceResult<Dictionary<string, string>>.CreateFailure("Invalid document object");
        }

        var metadata = new Dictionary<string, string>
        {
            ["SlideCount"] = presentation.Slides.Count.ToString(),
            ["Title"] = "PowerPoint Presentation"
        };

        // Calculate statistics
        var totalShapes = 0;
        var totalTextShapes = 0;

        foreach (ISlide slide in presentation.Slides)
        {
            totalShapes += slide.Shapes.Count;
            totalTextShapes += slide.Shapes.Count(s => s.TextBox is not null);
        }

        metadata["TotalShapes"] = totalShapes.ToString();
        metadata["TotalTextShapes"] = totalTextShapes.ToString();
        metadata["AverageShapesPerSlide"] = (totalShapes / (double)presentation.Slides.Count).ToString("F2");

        return await Task.FromResult(ServiceResult<Dictionary<string, string>>.CreateSuccess(metadata));
    }

    private async Task<ServiceResult<object>> ExtractPowerPointStructuredContentAsync(LoadedDocument document)
    {
        if (document.DocumentObject is not Presentation presentation)
        {
            return ServiceResult<object>.CreateFailure("Invalid document object");
        }

        var slides = new List<Dictionary<string, object>>();

        foreach (ISlide slide in presentation.Slides)
        {
            var slideData = new Dictionary<string, object>
            {
                ["SlideNumber"] = slide.Number,
                ["ShapeCount"] = slide.Shapes.Count
            };

            var shapes = new List<Dictionary<string, object>>();
            foreach (IShape shape in slide.Shapes)
            {
                var shapeData = new Dictionary<string, object>
                {
                    ["HasText"] = shape.TextBox is not null
                };

                if (shape.TextBox is not null)
                {
                    shapeData["Text"] = shape.TextBox.Text;
                }

                shapes.Add(shapeData);
            }

            slideData["Shapes"] = shapes;
            slides.Add(slideData);
        }

        var structuredContent = new Dictionary<string, object>
        {
            ["PresentationInfo"] = new
            {
                SlideCount = presentation.Slides.Count
            },
            ["Slides"] = slides
        };

        return await Task.FromResult(ServiceResult<object>.CreateSuccess(structuredContent));
    }

    #endregion
}
