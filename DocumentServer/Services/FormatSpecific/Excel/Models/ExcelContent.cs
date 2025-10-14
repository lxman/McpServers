namespace DocumentServer.Services.FormatSpecific.Excel.Models;

public class ExcelContent
{
    public List<ExcelWorksheet> Worksheets { get; set; } = [];
    public List<ExcelTable> Tables { get; set; } = [];
}
