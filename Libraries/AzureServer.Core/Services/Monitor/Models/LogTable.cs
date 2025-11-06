namespace AzureServer.Core.Services.Monitor.Models;

public class LogTable
{
    public string Name { get; set; } = string.Empty;
    public List<LogColumn> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}