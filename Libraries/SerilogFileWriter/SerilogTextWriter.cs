using System.Text;
using Serilog;
using Serilog.Events;

namespace SerilogFileWriter;

/// <summary>
/// Custom TextWriter that forwards all Console writes to Serilog.
/// This allows Console.WriteLine and Console.Error.WriteLine to be captured
/// and logged through Serilog, which is essential for MCP servers where
/// STDOUT/STDERR must remain clean for the JSON-RPC protocol.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// Log.Logger = new LoggerConfiguration()
///     .WriteTo.File("logs/server.log")
///     .CreateLogger();
///
/// Console.SetOut(new SerilogTextWriter(Log.Logger));
/// Console.SetError(new SerilogTextWriter(Log.Logger, LogEventLevel.Error));
/// </code>
/// </remarks>
public class SerilogTextWriter : TextWriter
{
    private readonly ILogger _logger;
    private readonly LogEventLevel _logLevel;
    private readonly StringBuilder _lineBuffer = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Creates a new SerilogTextWriter that forwards writes to Serilog.
    /// </summary>
    /// <param name="logger">The Serilog ILogger instance to write to</param>
    /// <param name="logLevel">The log level to use (default: Information for Console.Out, Error for Console.Error)</param>
    public SerilogTextWriter(ILogger logger, LogEventLevel logLevel = LogEventLevel.Information)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logLevel = logLevel;
    }

    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <summary>
    /// Writes a single character to the buffer.
    /// Flushes on newline character.
    /// </summary>
    public override void Write(char value)
    {
        lock (_lock)
        {
            if (value == '\n')
            {
                FlushInternal();
            }
            else if (value != '\r') // Ignore CR, handle only LF
            {
                _lineBuffer.Append(value);
            }
        }
    }

    /// <summary>
    /// Writes a complete line directly to Serilog.
    /// </summary>
    public override void WriteLine(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        lock (_lock)
        {
            _logger.Write(_logLevel, "[Console] {Message}", value);
        }
    }

    /// <summary>
    /// Flushes any buffered characters to Serilog.
    /// </summary>
    public override void Flush()
    {
        lock (_lock)
        {
            FlushInternal();
        }
    }

    /// <summary>
    /// Internal flush implementation (assumes lock is already held).
    /// </summary>
    private void FlushInternal()
    {
        if (_lineBuffer.Length <= 0) return;
        _logger.Write(_logLevel, "[Console] {Message}", _lineBuffer.ToString());
        _lineBuffer.Clear();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
            {
                FlushInternal();
            }
        }
        base.Dispose(disposing);
    }
}
