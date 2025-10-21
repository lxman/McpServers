using AzureServer.Services.Monitor.Models;

namespace AzureServer.Extensions;

public static class LogQueryResultExtensions
{
    public static int TotalRows(this LogQueryResult result)
    {
        return result.Tables?.Sum(t => t.Rows?.Count ?? 0) ?? 0;
    }
}