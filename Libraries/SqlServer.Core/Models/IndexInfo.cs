namespace SqlServer.Core.Models;

public class IndexInfo
{
    public required string IndexName { get; set; }
    public required string TableName { get; set; }
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public required IEnumerable<string> Columns { get; set; }
}
