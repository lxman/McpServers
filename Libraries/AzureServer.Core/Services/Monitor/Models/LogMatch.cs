namespace AzureServer.Core.Services.Monitor.Models;

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