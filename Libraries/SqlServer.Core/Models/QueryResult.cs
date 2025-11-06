namespace SqlServer.Core.Models;

public class QueryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IEnumerable<dynamic>? Data { get; set; }
    public int RowsAffected { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool IsTruncated { get; set; }
    public object? ScalarValue { get; set; }
}
