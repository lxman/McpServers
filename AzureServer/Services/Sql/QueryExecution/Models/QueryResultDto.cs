namespace AzureServer.Services.Sql.QueryExecution.Models;

/// <summary>
/// Represents the result of a SQL query execution
/// </summary>
public class QueryResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> ColumnNames { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
    public int RowsAffected { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string? ErrorMessage { get; set; }
}
