namespace SqlMcp.Models;

public class ColumnInfo
{
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public int? MaxLength { get; set; }
    public string? DefaultValue { get; set; }
}
