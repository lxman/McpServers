namespace AzureServer.Core.Services.Monitor.Models;
using AzureServer.Core.Common.Models;

public class LogQueryResult
{
    public List<LogTable> Tables { get; set; } = [];
    public Dictionary<string, object> Statistics { get; set; } = new();
    public string? Error { get; set; }
    
    /// <summary>
    /// Metadata about the query execution
    /// </summary>
    public QueryMetadata? Metadata { get; set; }
    
    /// <summary>
    /// Format analysis for log messages
    /// </summary>
    public LogFormatAnalysis? FormatAnalysis { get; set; }
    
    /// <summary>
    /// Pagination metadata for the query results
    /// </summary>
    public PaginationMetadata? Pagination { get; set; }
}