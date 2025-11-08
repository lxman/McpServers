using ClosedXML.Excel;
using DocumentServer.Core.Models.Common;
using DocumentServer.Core.Services.Core;
using DocumentServer.Core.Services.FormatSpecific.Excel.Models;
using Microsoft.Extensions.Logging;

namespace DocumentServer.Core.Services.FormatSpecific.Excel;

/// <summary>
/// Service for reading specific worksheets from Excel documents
/// </summary>
public class ExcelWorksheetReader(
    ILogger<ExcelWorksheetReader> logger,
    DocumentCache cache,
    PasswordManager passwordManager,
    DocumentDecryptionService decryptionService)
{
    /// <summary>
    /// Read a specific worksheet by name or index
    /// </summary>
    public async Task<ServiceResult<ExcelWorksheet>> ReadWorksheetAsync(
        string filePath,
        string? worksheetName = null,
        int? worksheetIndex = null)
    {
        logger.LogInformation("Reading worksheet from: {FilePath}, Name: {Name}, Index: {Index}",
            filePath, worksheetName ?? "null", worksheetIndex);

        try
        {
            // Try to get from cache first
            var cached = cache.Get(filePath);
            var workbook = cached?.DocumentObject as XLWorkbook;

            if (workbook is null)
            {
                logger.LogDebug("Document not in cache, loading: {FilePath}", filePath);
                
                var password = passwordManager.GetPasswordForFile(filePath);
                await using var fileStream = File.OpenRead(filePath);
                await using var decryptedStream = await decryptionService.DecryptDocumentAsync(fileStream, password);
                
                workbook = new XLWorkbook(decryptedStream);
            }

            IXLWorksheet? worksheet = null;

            // Find worksheet by name or index
            if (!string.IsNullOrWhiteSpace(worksheetName))
            {
                worksheet = workbook.Worksheets.FirstOrDefault(w => 
                    w.Name.Equals(worksheetName, StringComparison.OrdinalIgnoreCase));
                
                if (worksheet is null)
                {
                    return ServiceResult<ExcelWorksheet>.CreateFailure(
                        $"Worksheet '{worksheetName}' not found");
                }
            }
            else if (worksheetIndex.HasValue)
            {
                if (worksheetIndex.Value < 1 || worksheetIndex.Value > workbook.Worksheets.Count)
                {
                    return ServiceResult<ExcelWorksheet>.CreateFailure(
                        $"Worksheet index {worksheetIndex.Value} out of range (1-{workbook.Worksheets.Count})");
                }
                
                worksheet = workbook.Worksheet(worksheetIndex.Value);
            }
            else
            {
                // Default to first worksheet
                worksheet = workbook.Worksheets.First();
            }

            var excelWorksheet = await ConvertWorksheetAsync(worksheet);

            logger.LogInformation("Successfully read worksheet: {Name} with {Rows} rows and {Cols} columns",
                excelWorksheet.Name, excelWorksheet.RowCount, excelWorksheet.ColumnCount);

            return ServiceResult<ExcelWorksheet>.CreateSuccess(excelWorksheet);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading worksheet from: {FilePath}", filePath);
            return ServiceResult<ExcelWorksheet>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Get list of all worksheet names in an Excel file
    /// </summary>
    public async Task<ServiceResult<List<string>>> GetWorksheetNamesAsync(string filePath)
    {
        logger.LogInformation("Getting worksheet names from: {FilePath}", filePath);

        try
        {
            var cached = cache.Get(filePath);
            var workbook = cached?.DocumentObject as XLWorkbook;

            if (workbook is null)
            {
                var password = passwordManager.GetPasswordForFile(filePath);
                await using var fileStream = File.OpenRead(filePath);
                await using var decryptedStream = await decryptionService.DecryptDocumentAsync(fileStream, password);
                
                workbook = new XLWorkbook(decryptedStream);
            }

            var names = workbook.Worksheets.Select(w => w.Name).ToList();

            logger.LogInformation("Found {Count} worksheets in: {FilePath}", names.Count, filePath);

            return ServiceResult<List<string>>.CreateSuccess(names);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting worksheet names from: {FilePath}", filePath);
            return ServiceResult<List<string>>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private static async Task<ExcelWorksheet> ConvertWorksheetAsync(IXLWorksheet worksheet)
    {
        return await Task.Run(() =>
        {
            var excelWorksheet = new ExcelWorksheet
            {
                Name = worksheet.Name,
                Index = worksheet.Position,
                RowCount = worksheet.RangeUsed()?.RowCount() ?? 0,
                ColumnCount = worksheet.RangeUsed()?.ColumnCount() ?? 0,
                IsVisible = worksheet.Visibility == XLWorksheetVisibility.Visible
            };

            var usedRange = worksheet.RangeUsed();
            if (usedRange is not null)
            {
                foreach (var cell in usedRange.Cells())
                {
                    var excelCell = new ExcelCell
                    {
                        Address = cell.Address.ToString() ?? "",
                        Row = cell.Address.RowNumber,
                        Column = cell.Address.ColumnNumber,
                        Value = cell.Value.IsBlank ? null : cell.Value.ToString(),
                        Formula = cell.HasFormula ? cell.FormulaA1 : null,
                        DataType = GetCellDataType(cell),
                        Style = GetCellStyle(cell)
                    };
                    
                    excelWorksheet.Cells.Add(excelCell);
                }

                // Extract tables
                foreach (var table in worksheet.Tables)
                {
                    var excelTable = new ExcelTable
                    {
                        Name = table.Name,
                        Range = table.RangeAddress.ToString() ?? string.Empty,
                        RowCount = table.RowCount(),
                        ColumnCount = table.ColumnCount()
                    };
                    excelWorksheet.Tables.Add(excelTable);
                }
            }

            return excelWorksheet;
        });
    }

    private static string GetCellDataType(IXLCell cell)
    {
        return cell.DataType switch
        {
            XLDataType.Text => "String",
            XLDataType.Number => "Number",
            XLDataType.Boolean => "Boolean",
            XLDataType.DateTime => "DateTime",
            XLDataType.TimeSpan => "TimeSpan",
            XLDataType.Error => "Error",
            _ => "Unknown"
        };
    }

    private static string GetCellStyle(IXLCell cell)
    {
        var styleInfo = new List<string>();

        if (cell.Style.Font.Bold)
            styleInfo.Add("Bold");
        if (cell.Style.Font.Italic)
            styleInfo.Add("Italic");
        if (cell.Style.Font.Underline != XLFontUnderlineValues.None)
            styleInfo.Add("Underline");
        if (cell.Style.Fill.BackgroundColor.ColorType != XLColorType.Indexed || 
            cell.Style.Fill.BackgroundColor.Indexed != 64)
            styleInfo.Add($"BgColor:{cell.Style.Fill.BackgroundColor}");

        return styleInfo.Count > 0 ? string.Join(", ", styleInfo) : "Normal";
    }

    #endregion
}
