using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DesktopDriver.Services;

public class AuditLogger
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly string _auditLogPath;
    private readonly object _lockObject = new();

    public AuditLogger(ILogger<AuditLogger> logger)
    {
        _logger = logger;
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopDriver", "logs");
        Directory.CreateDirectory(logDir);
        _auditLogPath = Path.Combine(logDir, $"audit_{DateTime.Now:yyyyMMdd}.json");
    }

    public void LogOperation(string operation, string details, bool success, string? error = null)
    {
        var logEntry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Operation = operation,
            Details = details,
            Success = success,
            Error = error,
            ProcessId = Environment.ProcessId,
            UserName = Environment.UserName,
            MachineName = Environment.MachineName
        };

        try
        {
            lock (_lockObject)
            {
                var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
                File.AppendAllText(_auditLogPath, json + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log entry");
        }
    }

    public void LogFileOperation(string operation, string filePath, bool success, string? error = null)
    {
        LogOperation($"File_{operation}", $"Path: {filePath}", success, error);
    }

    public void LogCommandExecution(string command, bool success, string? error = null)
    {
        LogOperation("Command_Execution", $"Command: {command}", success, error);
    }

    public void LogProcessOperation(string operation, int? processId, bool success, string? error = null)
    {
        LogOperation($"Process_{operation}", $"PID: {processId}", success, error);
    }

    private class AuditLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int ProcessId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
    }
}
