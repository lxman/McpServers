namespace OfficeMcp.Models;

public class ExtractedTable
{
    public string Source { get; set; } = "";
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<string> Headers { get; set; } = [];
    public List<List<string>> Data { get; set; } = [];
}