using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using OfficeMcp.Models.Excel;

namespace OfficeMcp.Services;

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
            
            if (fileExtension is ".xlsx" or ".xlsm")
            {
                try
                {
                    using var xlWorkbook = new XLWorkbook(decryptedStream);
                    return await ConvertFromClosedXmlAsync(xlWorkbook);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "ClosedXML failed, falling back to NPOI for: {FilePath}", filePath);
                    decryptedStream.Position = 0;
                    IWorkbook workbook = await LoadWithNpoiAsync(decryptedStream, fileExtension);
                    return await ConvertFromNpoiAsync(workbook);
                }
            }
            else
            {
                // Use NPOI for .xls files
                IWorkbook workbook = await LoadWithNpoiAsync(decryptedStream, fileExtension);
                return await ConvertFromNpoiAsync(workbook);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading Excel content from: {FilePath}", filePath);
            return excelContent; // Return empty content rather than throwing
        }
    }

    private static async Task<IWorkbook> LoadWithNpoiAsync(Stream documentStream, string fileExtension)
    {
        return await Task.Run<IWorkbook>(() =>
        {
            if (fileExtension is ".xlsx" or ".xlsm")
            {
                return new XSSFWorkbook(documentStream);
            }
            return new HSSFWorkbook(documentStream);
        });
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
                }
                
                excelContent.Worksheets.Add(excelWorksheet);
            }
            
            return excelContent;
        });
    }

    private static async Task<ExcelContent> ConvertFromNpoiAsync(IWorkbook npoiWorkbook)
    {
        return await Task.Run(() =>
        {
            var excelContent = new ExcelContent();
            
            for (var i = 0; i < npoiWorkbook.NumberOfSheets; i++)
            {
                ISheet? sheet = npoiWorkbook.GetSheetAt(i);
                var excelWorksheet = new ExcelWorksheet
                {
                    Name = sheet.SheetName,
                    Index = i,
                    RowCount = sheet.LastRowNum + 1,
                    ColumnCount = GetMaxColumnCount(sheet),
                    IsVisible = !npoiWorkbook.IsSheetHidden(i)
                };

                // Extract all cells with data
                for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow? row = sheet.GetRow(rowIndex);
                    if (row == null) continue;
                    
                    for (int colIndex = row.FirstCellNum; colIndex < row.LastCellNum; colIndex++)
                    {
                        ICell? cell = row.GetCell(colIndex);
                        if (cell == null) continue;
                        
                        var excelCell = new ExcelCell
                        {
                            Address = $"{GetColumnLetter(colIndex)}{rowIndex + 1}",
                            Row = rowIndex + 1,
                            Column = colIndex + 1,
                            Value = GetNpoiCellValue(cell),
                            Formula = cell.CellType == CellType.Formula ? cell.CellFormula : null,
                            DataType = GetNpoiCellDataType(cell),
                            Style = GetNpoiCellStyle(cell)
                        };
                        
                        excelWorksheet.Cells.Add(excelCell);
                    }
                }
                
                excelContent.Worksheets.Add(excelWorksheet);
            }
            
            return excelContent;
        });
    }

    private static string GetClosedXmlCellDataType(IXLCell cell)
    {
        if (cell.Value.IsBlank) return "Blank";
        if (cell.Value.IsNumber) return "Number";
        if (cell.Value.IsDateTime) return "DateTime";
        if (cell.Value.IsBoolean) return "Boolean";
        if (cell.Value.IsText) return "Text";
        return "Unknown";
    }

    private static string GetClosedXmlCellStyle(IXLCell cell)
    {
        var styles = new List<string>();
        if (cell.Style.Font.Bold) styles.Add("Bold");
        if (cell.Style.Font.Italic) styles.Add("Italic");
        if (cell.Style.Font.Underline != XLFontUnderlineValues.None) styles.Add("Underline");
        return string.Join(", ", styles);
    }

    private static string? GetNpoiCellValue(ICell cell)
    {
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell) 
                ? cell.DateCellValue.ToString("yyyy-MM-dd HH:mm:ss")
                : cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
            CellType.Blank => null,
            _ => cell.ToString()
        };
    }

    private static string GetNpoiCellDataType(ICell cell)
    {
        return cell.CellType switch
        {
            CellType.String => "Text",
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell) ? "DateTime" : "Number",
            CellType.Boolean => "Boolean", 
            CellType.Formula => "Formula",
            CellType.Blank => "Blank",
            _ => "Unknown"
        };
    }

    private static string GetNpoiCellStyle(ICell cell)
    {
        var styles = new List<string>();
        ICellStyle? cellStyle = cell.CellStyle;
        
        if (cellStyle?.GetFont(cell.Sheet.Workbook)?.IsBold == true) styles.Add("Bold");
        if (cellStyle?.GetFont(cell.Sheet.Workbook)?.IsItalic == true) styles.Add("Italic");
        
        return string.Join(", ", styles);
    }

    private static int GetMaxColumnCount(ISheet sheet)
    {
        var maxColumns = 0;
        for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            IRow? row = sheet.GetRow(rowIndex);
            if (row != null && row.LastCellNum > maxColumns)
            {
                maxColumns = row.LastCellNum;
            }
        }
        return maxColumns;
    }

    private static string GetColumnLetter(int columnIndex)
    {
        var columnLetter = "";
        while (columnIndex >= 0)
        {
            columnLetter = (char)('A' + (columnIndex % 26)) + columnLetter;
            columnIndex = columnIndex / 26 - 1;
        }
        return columnLetter;
    }
}