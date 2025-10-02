namespace OfficeMcp.Models.Excel;

public class ExcelWorksheet
{
    public string Name { get; set; } = "";
    public int Index { get; set; }
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<ExcelCell> Cells { get; set; } = [];
    public List<ExcelTable> Tables { get; set; } = [];
    public bool IsVisible { get; set; } = true;
}