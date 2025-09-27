namespace OfficeMcp.Models.Excel;

public class ExcelAnalysis
{
    public int WorksheetCount { get; set; }
    public int ChartCount { get; set; }
    public int TableCount { get; set; }
    public int TotalCells { get; set; }
    public int FormulaCount { get; set; }
    public bool HasMacros { get; set; }
    public List<string> WorksheetNames { get; set; } = [];
}