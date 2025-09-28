using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using NPOI.HSSF.Record.Crypto;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using OfficeMcp.Models.Excel;

namespace OfficeMcp.Services;

public interface IExcelService
{
    Task<ExcelContent> LoadExcelContentAsync(string filePath, string? password = null);
}

public class ExcelService(ILogger<ExcelService> logger) : IExcelService
{
    public async Task<ExcelContent> LoadExcelContentAsync(string filePath, string? password = null)
    {
        var excelContent = new ExcelContent();
        
        try
        {
            IWorkbook workbook;
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Use NPOI for password-protected files or as fallback
            if (!string.IsNullOrEmpty(password) || fileExtension == ".xls")
            {
                workbook = await LoadWithNpoiAsync(filePath, password);
            }
            else
            {
                // Try ClosedXML first for .xlsx files without passwords (faster)
                try
                {
                    using var xlWorkbook = new XLWorkbook(filePath);
                    return await ConvertFromClosedXmlAsync(xlWorkbook);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ClosedXML failed, falling back to NPOI for: {FilePath}", filePath);
                    workbook = await LoadWithNpoiAsync(filePath, password);
                }
            }
            
            return await ConvertFromNpoiAsync(workbook);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading Excel content from: {FilePath}", filePath);
            return excelContent; // Return empty content rather than throwing
        }
    }

    private async Task<IWorkbook> LoadWithNpoiAsync(string filePath, string? password = null)
    {
        return await Task.Run<IWorkbook>(() =>
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            
            if (fileExtension is ".xlsx" or ".xlsm")
            {
                var workbook = new XSSFWorkbook(fileStream);
                
                // NPOI doesn't directly support XLSX passwords in the constructor
                // but we can try to access a protected sheet to trigger password requirement
                if (!string.IsNullOrEmpty(password))
                {
                    // For XLSX, password protection is usually at sheet level
                    // This is a limitation - NPOI has better .xls password support
                    logger.LogWarning("XLSX password support is limited in NPOI");
                }
                
                return workbook;
            }
            if (!string.IsNullOrEmpty(password))
            {
                // NPOI has good support for .xls passwords
                Biff8EncryptionKey.CurrentUserPassword = password;
                try
                {
                    return new HSSFWorkbook(fileStream);
                }
                finally
                {
                    Biff8EncryptionKey.CurrentUserPassword = null; // Clear password
                }
            }
            return new HSSFWorkbook(fileStream);
        });
    }

    private static async Task<ExcelContent> ConvertFromClosedXmlAsync(XLWorkbook xlWorkbook)
    {
        return await Task.Run(() =>
        {
            var excelContent = new ExcelContent();
            
            foreach (var worksheet in xlWorkbook.Worksheets)
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
                if (usedRange != null)
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
                var sheet = npoiWorkbook.GetSheetAt(i);
                var excelWorksheet = new ExcelWorksheet
                {
                    Name = sheet.SheetName,
                    Index = i,
                    RowCount = sheet.LastRowNum + 1,
                    ColumnCount = GetMaxColumnCount(sheet),
                    IsVisible = !npoiWorkbook.IsSheetHidden(i)
                };

                // Extract all cells with data
                for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row == null) continue;
                    
                    for (int colIndex = row.FirstCellNum; colIndex < row.LastCellNum; colIndex++)
                    {
                        var cell = row.GetCell(colIndex);
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
        var cellStyle = cell.CellStyle;
        
        if (cellStyle?.GetFont(cell.Sheet.Workbook)?.IsBold == true) styles.Add("Bold");
        if (cellStyle?.GetFont(cell.Sheet.Workbook)?.IsItalic == true) styles.Add("Italic");
        
        return string.Join(", ", styles);
    }

    private static int GetMaxColumnCount(ISheet sheet)
    {
        var maxColumns = 0;
        for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
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