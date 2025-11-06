namespace SqlServer.Core.Models;

public class TableInfo
{
    public required string TableName { get; set; }
    public string? Schema { get; set; }
    public string? TableType { get; set; }
    public long? RowCount { get; set; }
}
