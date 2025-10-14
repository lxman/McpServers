using ClosedXML.Excel;
using DocumentServer.Models.Common;
using DocumentServer.Services.Core;
using DocumentServer.Services.FormatSpecific.Excel.Models;
using MsOfficeCrypto;

namespace DocumentServer.Services.FormatSpecific.Excel;

/// <summary>
/// Service for extracting specific data ranges, formulas, and cell values from Excel documents
/// </summary>
public class ExcelDataExtractor(
    ILogger<ExcelDataExtractor> logger,
    DocumentCache cache,
    PasswordManager passwordManager)
{
    /// <summary>
    /// Extract a specific cell range from a worksheet
    /// </summary>
    /// <param name="filePath">Path to the Excel file</param>
    /// <param name="worksheetName">Worksheet name (optional, uses first if not specified)</param>
    /// <param name="rangeAddress">Range address (e.g., "A1:D10")</param>
    /// <returns>List of cells in the specified range</returns>
    public async Task<ServiceResult<List<ExcelCell>>> ExtractRangeAsync(
        string filePath,
        string rangeAddress,
        string? worksheetName = null)
    {
        logger.LogInformation("Extracting range {Range} from {FilePath}, Sheet: {Sheet}",
            rangeAddress, filePath, worksheetName ?? "first");

        try
        {
            IXLWorksheet worksheet = await GetWorksheetAsync(filePath, worksheetName);

            IXLRange range = worksheet.Range(rangeAddress);
            var cells = new List<ExcelCell>();

            foreach (IXLCell cell in range.Cells())
            {
                cells.Add(new ExcelCell
                {
                    Address = cell.Address.ToString() ?? "",
                    Row = cell.Address.RowNumber,
                    Column = cell.Address.ColumnNumber,
                    Value = cell.Value.IsBlank ? null : cell.Value.ToString(),
                    Formula = cell.HasFormula ? cell.FormulaA1 : null,
                    DataType = GetCellDataType(cell),
                    Style = GetCellStyle(cell)
                });
            }

            logger.LogInformation("Extracted {Count} cells from range {Range}", cells.Count, rangeAddress);

            return ServiceResult<List<ExcelCell>>.CreateSuccess(cells);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting range {Range} from {FilePath}", rangeAddress, filePath);
            return ServiceResult<List<ExcelCell>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract all formulas from a worksheet
    /// </summary>
    public async Task<ServiceResult<List<ExcelCell>>> ExtractFormulasAsync(
        string filePath,
        string? worksheetName = null)
    {
        logger.LogInformation("Extracting formulas from {FilePath}, Sheet: {Sheet}",
            filePath, worksheetName ?? "first");

        try
        {
            IXLWorksheet worksheet = await GetWorksheetAsync(filePath, worksheetName);

            var formulaCells = new List<ExcelCell>();

            IXLRange? usedRange = worksheet.RangeUsed();
            if (usedRange is not null)
            {
                foreach (IXLCell cell in usedRange.Cells())
                {
                    if (cell.HasFormula)
                    {
                        formulaCells.Add(new ExcelCell
                        {
                            Address = cell.Address.ToString() ?? "",
                            Row = cell.Address.RowNumber,
                            Column = cell.Address.ColumnNumber,
                            Value = cell.Value.ToString(),
                            Formula = cell.FormulaA1,
                            DataType = GetCellDataType(cell),
                            Style = GetCellStyle(cell)
                        });
                    }
                }
            }

            logger.LogInformation("Found {Count} formulas in worksheet", formulaCells.Count);

            return ServiceResult<List<ExcelCell>>.CreateSuccess(formulaCells);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting formulas from {FilePath}", filePath);
            return ServiceResult<List<ExcelCell>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract a single cell value
    /// </summary>
    public async Task<ServiceResult<ExcelCell>> ExtractCellAsync(
        string filePath,
        string cellAddress,
        string? worksheetName = null)
    {
        logger.LogInformation("Extracting cell {Cell} from {FilePath}, Sheet: {Sheet}",
            cellAddress, filePath, worksheetName ?? "first");

        try
        {
            IXLWorksheet worksheet = await GetWorksheetAsync(filePath, worksheetName);

            IXLCell cell = worksheet.Cell(cellAddress);

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

            logger.LogInformation("Extracted cell {Cell}: Value={Value}, Formula={Formula}",
                cellAddress, excelCell.Value, excelCell.Formula ?? "none");

            return ServiceResult<ExcelCell>.CreateSuccess(excelCell);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting cell {Cell} from {FilePath}", cellAddress, filePath);
            return ServiceResult<ExcelCell>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract all tables from a worksheet
    /// </summary>
    public async Task<ServiceResult<List<ExcelTable>>> ExtractTablesAsync(
        string filePath,
        string? worksheetName = null)
    {
        logger.LogInformation("Extracting tables from {FilePath}, Sheet: {Sheet}",
            filePath, worksheetName ?? "first");

        try
        {
            IXLWorksheet worksheet = await GetWorksheetAsync(filePath, worksheetName);

            var tables = new List<ExcelTable>();

            foreach (IXLTable table in worksheet.Tables)
            {
                tables.Add(new ExcelTable
                {
                    Name = table.Name,
                    Range = table.RangeAddress.ToString() ?? string.Empty,
                    RowCount = table.RowCount(),
                    ColumnCount = table.ColumnCount()
                });
            }

            logger.LogInformation("Found {Count} tables in worksheet", tables.Count);

            return ServiceResult<List<ExcelTable>>.CreateSuccess(tables);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting tables from {FilePath}", filePath);
            return ServiceResult<List<ExcelTable>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract data from a specific row
    /// </summary>
    public async Task<ServiceResult<List<ExcelCell>>> ExtractRowAsync(
        string filePath,
        int rowNumber,
        string? worksheetName = null)
    {
        logger.LogInformation("Extracting row {Row} from {FilePath}, Sheet: {Sheet}",
            rowNumber, filePath, worksheetName ?? "first");

        try
        {
            IXLWorksheet worksheet = await GetWorksheetAsync(filePath, worksheetName);

            IXLRow row = worksheet.Row(rowNumber);
            var cells = new List<ExcelCell>();

            foreach (IXLCell cell in row.Cells())
            {
                if (!cell.Value.IsBlank)
                {
                    cells.Add(new ExcelCell
                    {
                        Address = cell.Address.ToString() ?? "",
                        Row = cell.Address.RowNumber,
                        Column = cell.Address.ColumnNumber,
                        Value = cell.Value.ToString(),
                        Formula = cell.HasFormula ? cell.FormulaA1 : null,
                        DataType = GetCellDataType(cell),
                        Style = GetCellStyle(cell)
                    });
                }
            }

            logger.LogInformation("Extracted {Count} cells from row {Row}", cells.Count, rowNumber);

            return ServiceResult<List<ExcelCell>>.CreateSuccess(cells);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting row {Row} from {FilePath}", rowNumber, filePath);
            return ServiceResult<List<ExcelCell>>.CreateFailure(ex);
        }
    }

    /// <summary>
    /// Extract data from a specific column
    /// </summary>
    public async Task<ServiceResult<List<ExcelCell>>> ExtractColumnAsync(
        string filePath,
        string columnLetter,
        string? worksheetName = null)
    {
        logger.LogInformation("Extracting column {Column} from {FilePath}, Sheet: {Sheet}",
            columnLetter, filePath, worksheetName ?? "first");

        try
        {
            IXLWorksheet worksheet = await GetWorksheetAsync(filePath, worksheetName);

            IXLColumn column = worksheet.Column(columnLetter);
            var cells = new List<ExcelCell>();

            foreach (IXLCell cell in column.Cells())
            {
                if (!cell.Value.IsBlank)
                {
                    cells.Add(new ExcelCell
                    {
                        Address = cell.Address.ToString() ?? "",
                        Row = cell.Address.RowNumber,
                        Column = cell.Address.ColumnNumber,
                        Value = cell.Value.ToString(),
                        Formula = cell.HasFormula ? cell.FormulaA1 : null,
                        DataType = GetCellDataType(cell),
                        Style = GetCellStyle(cell)
                    });
                }
            }

            logger.LogInformation("Extracted {Count} cells from column {Column}", cells.Count, columnLetter);

            return ServiceResult<List<ExcelCell>>.CreateSuccess(cells);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting column {Column} from {FilePath}", columnLetter, filePath);
            return ServiceResult<List<ExcelCell>>.CreateFailure(ex);
        }
    }

    #region Private Methods

    private async Task<IXLWorksheet> GetWorksheetAsync(string filePath, string? worksheetName)
    {
        LoadedDocument? cached = cache.Get(filePath);
        var workbook = cached?.DocumentObject as XLWorkbook;

        if (workbook is null)
        {
            logger.LogDebug("Document not in cache, loading: {FilePath}", filePath);
            
            string? password = passwordManager.GetPasswordForFile(filePath);
            
            // Use MsOfficeCrypto to handle decryption (or pass through if not encrypted)
            await using FileStream fileStream = File.OpenRead(filePath);
            await using Stream decryptedStream = await OfficeDocument.DecryptAsync(fileStream, password);
            
            // Copy to memory stream and load workbook
            var memoryStream = new MemoryStream();
            await decryptedStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            workbook = new XLWorkbook(memoryStream);
        }

        if (string.IsNullOrWhiteSpace(worksheetName))
        {
            return workbook.Worksheets.First();
        }

        IXLWorksheet? worksheet = workbook.Worksheets.FirstOrDefault(w => 
            w.Name.Equals(worksheetName, StringComparison.OrdinalIgnoreCase));

        if (worksheet is null)
        {
            throw new InvalidOperationException($"Worksheet '{worksheetName}' not found");
        }

        return worksheet;
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