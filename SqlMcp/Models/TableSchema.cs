namespace SqlMcp.Models;

public class TableSchema
{
    public required string TableName { get; set; }
    public string? Schema { get; set; }
    public required IEnumerable<ColumnInfo> Columns { get; set; }
}
