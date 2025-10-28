namespace SqlMcp.Models;

public class ForeignKeyInfo
{
    public required string ConstraintName { get; set; }
    public required string TableName { get; set; }
    public required string ColumnName { get; set; }
    public required string ReferencedTableName { get; set; }
    public required string ReferencedColumnName { get; set; }
    public string? DeleteRule { get; set; }
    public string? UpdateRule { get; set; }
}
