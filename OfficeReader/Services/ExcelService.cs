using ClosedXML.Excel;
using OfficeReader.Models.Excel;

namespace OfficeReader.Services;

public interface IExcelService
{
    Task<ExcelContent> LoadExcelContentAsync(string filePath, string? password = null);
}

public class ExcelService(IDocumentDecryptionService decryptionService, ILogger<ExcelService> logger) : IExcelService
{
    public async Task<ExcelContent> LoadExcelContentAsync(string filePath, string? password = null)
    {
        var excelContent = new ExcelContent();
        
        try
        {
            await using FileStream fileStream = File.OpenRead(filePath);
            await using Stream decryptedStream = await decryptionService.DecryptDocumentAsync(fileStream, password);
            
            string fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

            if (fileExtension is not (".xlsx" or ".xlsm"))
                throw new NotSupportedException(
                    $"Unsupported Excel format: {fileExtension}. Only .xlsx and .xlsm are supported.");
            using var xlWorkbook = new XLWorkbook(decryptedStream);
            return await ConvertFromClosedXmlAsync(xlWorkbook);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading Excel content from: {FilePath}", filePath);
            return excelContent; // Return empty content rather than throwing
        }
    }

    private static async Task<ExcelContent> ConvertFromClosedXmlAsync(XLWorkbook xlWorkbook)
    {
        return await Task.Run(() =>
        {
            var excelContent = new ExcelContent();
            
            foreach (IXLWorksheet worksheet in xlWorkbook.Worksheets)
            {
                var excelWorksheet = new ExcelWorksheet
                {
                    Name = worksheet.Name,
                    Index = worksheet.Position,
                    RowCount = worksheet.RangeUsed()?.RowCount() ?? 0,
                    ColumnCount = worksheet.RangeUsed()?.ColumnCount() ?? 0,
                    IsVisible = worksheet.Visibility == XLWorksheetVisibility.Visible
                };

                IXLRange? usedRange = worksheet.RangeUsed();
                if (usedRange != null)
                {
                    foreach (IXLCell? cell in usedRange.Cells())
                    {
                        var excelCell = new ExcelCell
                        {
                            Address = cell.Address.ToString() ?? "",
                            Row = cell.Address.RowNumber,
                            Column = cell.Address.ColumnNumber,
                            Value = cell.Value.IsBlank ? null : cell.Value.ToString(),
                            Formula = cell.HasFormula ? cell.FormulaA1 : null,
                            DataType = GetClosedXmlCellDataType(cell),
                            Style = GetClosedXmlCellStyle(cell)
                        };
                        
                        excelWorksheet.Cells.Add(excelCell);
                    }

                    // Extract tables
                    foreach (IXLTable table in worksheet.Tables)
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
                
                excelContent.Worksheets.Add(excelWorksheet);
            }
            
            return excelContent;
        });
    }

    private static string GetClosedXmlCellDataType(IXLCell cell)
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

    private static string GetClosedXmlCellStyle(IXLCell cell)
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
}