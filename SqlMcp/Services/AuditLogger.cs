using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SqlMcp.Services.Interfaces;

namespace SqlMcp.Services;

public class AuditLogger : IAuditLogger
{
    private readonly ConcurrentQueue<string> _auditLog = new();
    private readonly ILogger<AuditLogger> _logger;
    private const int MaxLogEntries = 1000;

    public AuditLogger(ILogger<AuditLogger> logger)
    {
        _logger = logger;
    }

    public Task LogQueryAsync(string connectionName, string sql, object? parameters, string result)
    {
        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Type = "Query",
            ConnectionName = connectionName,
            Sql = sql,
            Parameters = parameters,
            Result = result
        };

        AddLogEntry(JsonSerializer.Serialize(logEntry));
        return Task.CompletedTask;
    }

    public Task LogConnectionAsync(string connectionName, string action)
    {
        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Type = "Connection",
            ConnectionName = connectionName,
            Action = action
        };

        AddLogEntry(JsonSerializer.Serialize(logEntry));
        return Task.CompletedTask;
    }

    public Task LogTransactionAsync(string transactionId, string action)
    {
        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Type = "Transaction",
            TransactionId = transactionId,
            Action = action
        };

        AddLogEntry(JsonSerializer.Serialize(logEntry));
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetRecentLogsAsync(int count = 50)
    {
        IEnumerable<string> logs = _auditLog.TakeLast(count);
        return Task.FromResult(logs);
    }

    private void AddLogEntry(string entry)
    {
        _auditLog.Enqueue(entry);

        while (_auditLog.Count > MaxLogEntries)
        {
            _auditLog.TryDequeue(out _);
        }
    }
}
