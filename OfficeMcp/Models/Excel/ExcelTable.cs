namespace OfficeMcp.Models.Excel;

public class ExcelTable
{
    public string Name { get; set; } = "";
    public string Range { get; set; } = "";
    public string WorksheetName { get; set; } = "";
    public List<string> Headers { get; set; } = [];
    public int RowCount { get; set; }
}