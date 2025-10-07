namespace AzureMcp.Services.Monitor.Models;

public class LogQueryResult
{
    public List<LogTable> Tables { get; set; } = [];
    public Dictionary<string, object> Statistics { get; set; } = new();
    public string? Error { get; set; }
}

public class LogTable
{
    public string Name { get; set; } = string.Empty;
    public List<LogColumn> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}

public class LogColumn
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class LogMatch
{
    public string LogGroup { get; set; } = string.Empty;
    public string? LogStream { get; set; }
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ContextBefore { get; set; } = [];
    public List<string> ContextAfter { get; set; } = [];
    public int LineNumber { get; set; }
}
