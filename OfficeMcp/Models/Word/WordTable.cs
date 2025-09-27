namespace OfficeMcp.Models.Word;

public class WordTable
{
    public int TableNumber { get; set; }
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<List<string>> Cells { get; set; } = [];
    public List<string> Headers { get; set; } = [];
}