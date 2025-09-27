namespace OfficeMcp.Models.Excel;

public class ExcelProperties
{
    public int WorksheetCount { get; set; }
    public int ChartCount { get; set; }
    public int TableCount { get; set; }
    public bool HasMacros { get; set; }
    public bool HasExternalLinks { get; set; }
}