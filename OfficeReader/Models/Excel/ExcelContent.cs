namespace OfficeReader.Models.Excel;

public class ExcelContent
{
    public List<ExcelWorksheet> Worksheets { get; set; } = [];
    public List<ExcelChart> Charts { get; set; } = [];
    public List<ExcelTable> Tables { get; set; } = [];
    public ExcelProperties Properties { get; set; } = new();
}